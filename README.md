# Demo WorkerRole Azure Cloud Service 
WorkerRole to perform latency measurement in Azure Cloud Service for **Mongo API Support** powered by **Azure DocumentDB**

* Details:
 * Uses  C# Mongo SDK
 * Uses read preference = NEAREST for read latency measurement
 * Write latency is measured only from write region
 
## Getting started

1. Set up Azure Cosmos DB

   * Create an Azure Cosmos DB Account configured to use the MongoDb API.
   * Add 'East US' region as a write region
   * Add 'Australia East','South India','West Europe','East Asia' as read regions. 'East US' will be a read region as well by default
   * Create a database in it named 'nodetest' with single partition collections 'demometrics' and 'demodata'

   (this configuration is important since the front end is configured with this setting as well)
 
2. Clone / download the repo and open the LoadGenerator.sln file in Visual Studio

3. Configure Cosmos DB server settings in the worker App

    * Open the WorkerRole1/app.config file and modify the following key/value pairs to reflect your MongoDb settings

    ```xml
    <appSettings>
       <add key="MongoUserName" value="YOUR_MONGO_USER_NAME" /> 
       <add key="MongoPassword" value="YOUR_MONGO_PASSWORD" />
       <add key="MongoDefaultEndpoint" value="YOUR_MONGO_HOST" /> <!--example:my-mongo.documents.azure.com-->
       <add key="DocumentDbEndPoint" value="https:// followed by YOUR_MONGO_HOST" /> <!--example:https://my-mongo.documents.azure.com-->
    </appSettings>
    ```
4. Package the Cloud Service

    * Right click on the LoadGeneator project and choose 'Package'.
    * In 'service configuration', choose 'East US' and click 'Package'
    * LoadGenerator.cspkg (Cloud service package file) and ServiceConfiguration.East US.cscfg (Cloud service configuration file) are created abd placed within the app.publish folder.
    
5. Create and deploy Azure cloud service

   * Create an azure cloud service deployed at the 'East US' location.
   * Upload the LoadGenerator.cspkg and ServiceConfiguration.East US.cscfg file when prompted. The cloud service will deploy in a few minutes. Ensure that its status is 'Running'
   * Repeat steps 4 and 5 for the other 4 locations.

6. Deploy front end Web app

   * The source code and steps to deploy the frontend web app is [here](https://github.com/srinathnarayanan/mongotunabledemo-webapp)
   * The Front end web app to visualize latencies is deployed [here](http://mongotunabledemo.azurewebsites.net/)
