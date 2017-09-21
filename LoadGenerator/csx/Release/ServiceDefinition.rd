<?xml version="1.0" encoding="utf-8"?>
<serviceModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" name="LoadGenerator" generation="1" functional="0" release="0" Id="d49afb6b-6c02-4443-872b-47d6ac059805" dslVersion="1.2.0.0" xmlns="http://schemas.microsoft.com/dsltools/RDSM">
  <groups>
    <group name="LoadGeneratorGroup" generation="1" functional="0" release="0">
      <settings>
        <aCS name="WorkerRole1:CurrentRegion" defaultValue="">
          <maps>
            <mapMoniker name="/LoadGenerator/LoadGeneratorGroup/MapWorkerRole1:CurrentRegion" />
          </maps>
        </aCS>
        <aCS name="WorkerRole1:IsMasterWorker" defaultValue="">
          <maps>
            <mapMoniker name="/LoadGenerator/LoadGeneratorGroup/MapWorkerRole1:IsMasterWorker" />
          </maps>
        </aCS>
        <aCS name="WorkerRole1:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/LoadGenerator/LoadGeneratorGroup/MapWorkerRole1:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </maps>
        </aCS>
        <aCS name="WorkerRole1Instances" defaultValue="[1,1,1]">
          <maps>
            <mapMoniker name="/LoadGenerator/LoadGeneratorGroup/MapWorkerRole1Instances" />
          </maps>
        </aCS>
      </settings>
      <maps>
        <map name="MapWorkerRole1:CurrentRegion" kind="Identity">
          <setting>
            <aCSMoniker name="/LoadGenerator/LoadGeneratorGroup/WorkerRole1/CurrentRegion" />
          </setting>
        </map>
        <map name="MapWorkerRole1:IsMasterWorker" kind="Identity">
          <setting>
            <aCSMoniker name="/LoadGenerator/LoadGeneratorGroup/WorkerRole1/IsMasterWorker" />
          </setting>
        </map>
        <map name="MapWorkerRole1:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/LoadGenerator/LoadGeneratorGroup/WorkerRole1/Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </setting>
        </map>
        <map name="MapWorkerRole1Instances" kind="Identity">
          <setting>
            <sCSPolicyIDMoniker name="/LoadGenerator/LoadGeneratorGroup/WorkerRole1Instances" />
          </setting>
        </map>
      </maps>
      <components>
        <groupHascomponents>
          <role name="WorkerRole1" generation="1" functional="0" release="0" software="E:\mongoGeoDemo\LoadGenerator\LoadGenerator\csx\Release\roles\WorkerRole1" entryPoint="base\x64\WaHostBootstrapper.exe" parameters="base\x64\WaWorkerHost.exe " memIndex="-1" hostingEnvironment="consoleroleadmin" hostingEnvironmentVersion="2">
            <settings>
              <aCS name="CurrentRegion" defaultValue="" />
              <aCS name="IsMasterWorker" defaultValue="" />
              <aCS name="Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="" />
              <aCS name="__ModelData" defaultValue="&lt;m role=&quot;WorkerRole1&quot; xmlns=&quot;urn:azure:m:v1&quot;&gt;&lt;r name=&quot;WorkerRole1&quot; /&gt;&lt;/m&gt;" />
            </settings>
            <resourcereferences>
              <resourceReference name="DiagnosticStore" defaultAmount="[4096,4096,4096]" defaultSticky="true" kind="Directory" />
              <resourceReference name="EventStore" defaultAmount="[1000,1000,1000]" defaultSticky="false" kind="LogStore" />
            </resourcereferences>
          </role>
          <sCSPolicy>
            <sCSPolicyIDMoniker name="/LoadGenerator/LoadGeneratorGroup/WorkerRole1Instances" />
            <sCSPolicyUpdateDomainMoniker name="/LoadGenerator/LoadGeneratorGroup/WorkerRole1UpgradeDomains" />
            <sCSPolicyFaultDomainMoniker name="/LoadGenerator/LoadGeneratorGroup/WorkerRole1FaultDomains" />
          </sCSPolicy>
        </groupHascomponents>
      </components>
      <sCSPolicy>
        <sCSPolicyUpdateDomain name="WorkerRole1UpgradeDomains" defaultPolicy="[5,5,5]" />
        <sCSPolicyFaultDomain name="WorkerRole1FaultDomains" defaultPolicy="[2,2,2]" />
        <sCSPolicyID name="WorkerRole1Instances" defaultPolicy="[1,1,1]" />
      </sCSPolicy>
    </group>
  </groups>
</serviceModel>