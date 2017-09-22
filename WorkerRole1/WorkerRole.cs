using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using MongoDB.Driver;
using System.Security.Authentication;
using MongoDB.Bson;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using System.Configuration;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private const int CooldownIntervalinMS = 2000; //2 seconds
        private const int OperationCount = 1000;
        private const int MetricsRecordCount = 100;
        private const int PayloadLength = 1024;
        private const string ReadPayloadTypeStr = "readPayload";
        private const string WritePayloadTypeStr = "writePayload";

        private const string FindActionDescStr = "findAction";
        private const string InsertActionDescStr = "insertAction";

        //mongo instance details
        private const string DatabaseName = "nodetest";
        private const string DataCollectionName = "demodata";
        private const string MetricsCollectionName = "demometrics";

        //mongo user credentials
        private string MongoUserName = ConfigurationManager.AppSettings["MongoUserName"];
        private string MongoPassword = ConfigurationManager.AppSettings["MongoPassword"];
        private string MongoDefaultEndpoint = ConfigurationManager.AppSettings["MongoDefaultEndpoint"];
        private string DocumentDbEndPoint = ConfigurationManager.AppSettings["DocumentDbEndPoint"];
        private int MongoPort = Int32.Parse(ConfigurationManager.AppSettings["MongoPort"]);

        private TimeSpan MetricsCleanupInterval = new TimeSpan(0, 2, 0); //clean up metrics every 20 minutes
        private Stopwatch latencyWatch = new Stopwatch();

        //mongo clients
        private MongoClient defaultClient;

        //payload related
        private PayloadDocument insertPayloadDoc = new PayloadDocument(WorkerRole.PayloadLength, WorkerRole.WritePayloadTypeStr);
        private PayloadDocument readPayloadDoc = new PayloadDocument(WorkerRole.PayloadLength, WorkerRole.ReadPayloadTypeStr);

        //azure regions map
        private Dictionary<string, string> regionsMaps = new Dictionary<string, string>(); //region from endpoint -> official Azure region name

        private string currentRegion = RoleEnvironment.GetConfigurationSettingValue("CurrentRegion");
        private bool isWriteRegion = false;
        private bool isMasterWorker = Boolean.Parse(RoleEnvironment.GetConfigurationSettingValue("IsMasterWorker"));
        private DateTime lastMetricsCleanupTime = DateTime.MinValue;
        private DocumentClient client;
        private List<String> readRegions = new List<String>();
        private String writeRegion;
        public override void Run()
        {
            Trace.TraceInformation("WorkerRole1 is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at https://go.microsoft.com/fwlink/?LinkId=166357.

            //do mongo setup
            this.SetupMongoClient().Wait();

            //setup documentdb client to get read/write endpoints
            this.getReadWriteRegions().Wait();

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                //ismaster
                BsonDocument isMasterResponse = await GetIsMasterResponse();
                this.isWriteRegion = (this.currentRegion.Equals(this.writeRegion)) ? true : false;

                //measurement phase
                //latency actions
                IMongoDatabase dbr = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                IMongoCollection<BsonDocument> collr = dbr.GetCollection<BsonDocument>(WorkerRole.DataCollectionName);

                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("ud", this.readPayloadDoc.Ud);
                ReadPreference readPref = new ReadPreference(ReadPreferenceMode.Nearest);
                try
                {
                    double readLatency = this.MeasureLatencySafe(
                        () =>
                        {
                            var fResult = collr.WithReadPreference(readPref).Find<BsonDocument>(filter).FirstOrDefault<BsonDocument>();
                        }, WorkerRole.FindActionDescStr);
                
                Thread.Sleep(WorkerRole.CooldownIntervalinMS); //cool down

                double writeLatency = 0.0;
                if (isWriteRegion) //measure write latency only at write region
                {
                    IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                    IMongoCollection<PayloadDocument> coll = db.GetCollection<PayloadDocument>(WorkerRole.DataCollectionName);
                    writeLatency = this.MeasureLatencySafe(
                        () =>
                        {
                            coll.InsertOne(this.insertPayloadDoc);
                        }, WorkerRole.InsertActionDescStr);
                }

                //reporting phase
                //store latencies
                await this.StoreLatencies(writeLatency, readLatency, this.currentRegion);
                }
                catch (Exception e)
                {
                }
                //cleanup phase
                await this.Cleanup();
                //store worker region
                await this.StoreRegion();
                if (this.isMasterWorker)
                {
                    //store account regions
                    await this.StoreAccountRegions(isMasterResponse);
                }

                if (DateTime.UtcNow - this.lastMetricsCleanupTime > this.MetricsCleanupInterval)
                {
                    //cleanup old metrics
                    await this.CleanupMetrics();
                    this.lastMetricsCleanupTime = DateTime.UtcNow;
                }

                Thread.Sleep(WorkerRole.CooldownIntervalinMS); //cool down

            }
        }

        private double MeasureLatency(Action action)
        {
            double latency;
            List<double> latencies = new List<Double>();

            for (int i = 0; i < WorkerRole.OperationCount; i++)
            {
                this.latencyWatch.Restart();
                action();
                this.latencyWatch.Stop();
                latencies.Add(latencyWatch.ElapsedMilliseconds * 1.0);
            }

            latency = this.GetP99Latency(latencies);
            return latency;
        }

        private double MeasureLatencySafe(Action action, string actionDesc)
        {
            try
            {
                return MeasureLatency(action);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Latency action failed for action desc: {0} exception:{1}", actionDesc, ex.ToString());
            }

            return 0.0;
        }

        private async Task Cleanup()
        {
            //delete all insert docs
            IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
            IMongoCollection<PayloadDocument> coll = db.GetCollection<PayloadDocument>(WorkerRole.DataCollectionName);
            var builder = Builders<PayloadDocument>.Filter;
            var filter = builder.Eq("Type", this.insertPayloadDoc.Type);
            try
            {
                await coll.DeleteManyAsync(filter);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Cleanup failed ex: {0}, full ex:{1}", ex.GetType(), ex.ToString());
            }

        }

        private async Task CleanupMetrics()
        {
            const string TypeField = "type";
            const string LatencyInfoTypeStr = "latencyInfo";
            const string RegionField = "region";

            try
            {
                IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                IMongoCollection<BsonDocument> coll = db.GetCollection<BsonDocument>(WorkerRole.MetricsCollectionName);

                var builder = Builders<BsonDocument>.Filter;
                var filter1 = builder.Eq(TypeField, LatencyInfoTypeStr);
                var filter2 = builder.Eq(RegionField, this.currentRegion);
                List<FilterDefinition<BsonDocument>> flist = new List<FilterDefinition<BsonDocument>>();
                flist.Add(filter1);
                flist.Add(filter2);
                var filter = builder.And(flist);
                var sort = Builders<BsonDocument>.Sort.Descending("_id");

                long rcount = coll.Find(filter).Count();
                if (rcount > WorkerRole.MetricsRecordCount)
                {
                    var doc = coll.Find(filter).Sort(sort).Skip(WorkerRole.MetricsRecordCount).FirstOrDefault<BsonDocument>();
                    BsonValue id;
                    if (doc.TryGetValue("_id", out id))
                    {
                        var dbuilder = Builders<BsonDocument>.Filter;
                        var dfilter1 = builder.Lt<ObjectId>("_id", id.AsObjectId);
                        var dfilter2 = builder.Eq(TypeField, LatencyInfoTypeStr);
                        var dfilter3 = builder.Eq(RegionField, this.currentRegion);
                        List<FilterDefinition<BsonDocument>> dlist = new List<FilterDefinition<BsonDocument>>();
                        dlist.Add(dfilter1);
                        dlist.Add(dfilter2);
                        dlist.Add(dfilter3);
                        var dfilter = builder.And(dlist);
                        var dresult = await coll.DeleteManyAsync(dfilter);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Metrics cleanup failed exception - {0}", ex.ToString());
            }
        }

        private double GetP99Latency(List<Double> latencies)
        {
            latencies.Sort();
            int N = latencies.Count;
            int p99Index = (99 * N / 100) - 1;
            return latencies[p99Index];
        }

        private async Task StoreLatencies(double writeLatency, double readLatency, string region)
        {
            const string WriteLatencyField = "writeLatency";
            const string ReadLatencyField = "readLatency";
            const string RegionField = "region";
            const string TypeField = "type";
            const string LatencyInfoTypeStr = "latencyInfo";

            try
            {
                IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                IMongoCollection<BsonDocument> coll = db.GetCollection<BsonDocument>(WorkerRole.MetricsCollectionName);


                //store latency info
                BsonDocument latencyInfoDocument = new BsonDocument
                {
                    {TypeField, LatencyInfoTypeStr},
                    {WriteLatencyField, writeLatency },
                    {ReadLatencyField, readLatency },
                    {RegionField, region }
                };

                await coll.InsertOneAsync(latencyInfoDocument);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Storing latencies failed exception : {0}", ex.ToString());
            }

        }

        private async Task StoreRegion()
        {
            const string TypeField = "type";
            const string RegionInfoTypeStr = "regionInfo";
            const string RegionField = "region";
            const string IsWriteRegionField = "iswriteregion";
            string regionVal = this.currentRegion;
            try
            {
                IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                IMongoCollection<BsonDocument> coll = db.GetCollection<BsonDocument>(WorkerRole.MetricsCollectionName);

                //update region info
                BsonDocument regionInfoDocument = new BsonDocument
                {
                    {TypeField, RegionInfoTypeStr },
                    {RegionField, regionVal },
                    {IsWriteRegionField, this.isWriteRegion }
                };

                var builder = Builders<BsonDocument>.Filter;
                var filter1 = builder.Eq(TypeField, RegionInfoTypeStr);
                var filter2 = builder.Eq(RegionField, regionVal);
                List<FilterDefinition<BsonDocument>> flist = new List<FilterDefinition<BsonDocument>>();
                flist.Add(filter1);
                flist.Add(filter2);
                var filter = builder.And(flist);
                FindOneAndReplaceOptions<BsonDocument> options = new FindOneAndReplaceOptions<BsonDocument>();
                options.IsUpsert = true;
                await coll.FindOneAndReplaceAsync<BsonDocument>(filter, regionInfoDocument, options);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Storing worker role region failed  exception ; {0}", ex.ToString());
            }
        }

        private async Task SetupMongoClient()
        {
            try
            {
                this.defaultClient = new MongoClient(this.GetMongoClientSettingsHelper(MongoDefaultEndpoint));

                //insert read payload document once to be used by read operations
                IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                IMongoCollection<PayloadDocument> coll = db.GetCollection<PayloadDocument>(WorkerRole.DataCollectionName);
                await coll.InsertOneAsync(this.readPayloadDoc);
            }
            catch (Exception ex)
            {
                Trace.TraceError("setupMongoClient Failed with exception: {0} full exception: {1}", ex.GetType(), ex.ToString());
            }
        }

        private MongoClientSettings GetMongoClientSettingsHelper(string endpoint)
        {
            const string MongoAuthScramSHATypeString = "SCRAM-SHA-1";

            string mongoHost = endpoint;
            string mongoUsername = MongoUserName;
            string mongoPassword = MongoPassword;

            MongoClientSettings settings = new MongoClientSettings();
            settings.Server = new MongoServerAddress(mongoHost, MongoPort);
            settings.UseSsl = true;
            settings.VerifySslCertificate = true;
            settings.SslSettings = new SslSettings();
            settings.SslSettings.EnabledSslProtocols = SslProtocols.Tls12;

            settings.ConnectionMode = MongoDB.Driver.ConnectionMode.ReplicaSet;
            settings.ReplicaSetName = "globaldb";

            MongoIdentity identity = new MongoInternalIdentity(WorkerRole.DatabaseName, mongoUsername);
            MongoIdentityEvidence evidence = new PasswordEvidence(mongoPassword);

            settings.Credentials = new List<MongoCredential>()
            {
                new MongoCredential(MongoAuthScramSHATypeString, identity, evidence)
            };

            return settings;
        }

        private async Task getReadWriteRegions()
        {
            this.client = new DocumentClient(new Uri(DocumentDbEndPoint), MongoPassword, null, null);
            //get read and write regions
            DatabaseAccount dba = await client.GetDatabaseAccountAsync();
            foreach (DatabaseAccountLocation loc in dba.ReadableLocations)
            {
                readRegions.Add(loc.Name);
            }
            writeRegion = dba.WritableLocations.ElementAt(0).Name;
        }

        private async Task StoreAccountRegions(BsonDocument isMasterResponse)
        {
            const string TypeField = "type";
            const string AccRegionInfoTypeStr = "AccRegionInfo";
            const string RegionsField = "regions";

            BsonDocument doc = new BsonDocument
            {
                {TypeField, AccRegionInfoTypeStr},
                {RegionsField, new BsonArray(this.readRegions)}
            };

            try
            {
                IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                IMongoCollection<BsonDocument> coll = db.GetCollection<BsonDocument>(WorkerRole.MetricsCollectionName);

                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq(TypeField, AccRegionInfoTypeStr);
                FindOneAndReplaceOptions<BsonDocument> options = new FindOneAndReplaceOptions<BsonDocument>();
                options.IsUpsert = true;
                await coll.FindOneAndReplaceAsync<BsonDocument>(filter, doc, options);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Storing Account regions failed exception : {0}", ex.ToString());
            }

        }

        private async Task<BsonDocument> GetIsMasterResponse()
        {
            BsonDocument isMasterReponse = null;
            try
            {
                //run isMaster command using default client
                IMongoDatabase db = this.defaultClient.GetDatabase(WorkerRole.DatabaseName);
                var command = new BsonDocumentCommand<BsonDocument>(new BsonDocument
                    {
                        {"isMaster", 1}
                    });

                isMasterReponse = await db.RunCommandAsync(command);
            }
            catch (Exception ex)
            {
                Trace.TraceError("IsMaster failed  exception {0}, full exception - {1}", ex.GetType(), ex.ToString());
            }

            return isMasterReponse;
        }
    }
}
