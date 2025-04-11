using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Oss.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using Newtonsoft.Json;

public partial class APS
{
    DesignAutomationClient _api;

    public DesignAutomationClient GetClient()
    {
        if (_api == null)
            _api = new DesignAutomationClient();

        return _api;
    }

    private dynamic EngineAttributes(string engine)
    {
        if (engine.Contains("3dsMax")) return new { commandLine = "$(engine.path)\\3dsmaxbatch.exe -sceneFile \"$(args[inputFile].path)\" $(settings[script].path)", extension = "max", script = "da = dotNetClass(\"Autodesk.Forge.Sample.DesignAutomation.Max.RuntimeExecute\")\nda.ModifyWindowWidthHeight()\n" };
        if (engine.Contains("AutoCAD")) return new { commandLine = "$(engine.path)\\accoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{0}].path)\" /s $(settings[script].path)", extension = "dwg", script = "UpdateParam\n" };
        if (engine.Contains("Inventor")) return new { commandLine = "$(engine.path)\\inventorcoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{0}].path)\"", extension = "ipt", script = string.Empty };
        if (engine.Contains("Revit")) return new { commandLine = "$(engine.path)\\revitcoreconsole.exe /i \"$(args[inputFile].path)\" /al \"$(appbundles[{0}].path)\"", extension = "rvt", script = string.Empty };
        throw new Exception("Invalid engine");
    }


    public async Task<dynamic> Setup(JObject setupSpecs, string localBundlesFolder)
    {
        dynamic appBundle = await CreateAppBundle(setupSpecs, localBundlesFolder);
        dynamic activity = await CreateActivity(setupSpecs);

        return new { AppBundle = appBundle.AppBundle, Version = appBundle.Version, Activity = activity.Activity };
    }


    public async Task<dynamic> CreateAppBundle(JObject setupSpecs, string localBundlesFolder)
    {
        // basic input validation
        string zipFileName = setupSpecs["zipFileName"].Value<string>();
        string engineName = setupSpecs["engine"].Value<string>();

        // standard name for this sample
        string appBundleName = zipFileName + "AppBundle";

        // check if ZIP with bundle is here
        string packageZipPath = Path.Combine(localBundlesFolder, zipFileName + ".zip");
        if (!System.IO.File.Exists(packageZipPath)) throw new Exception("Appbundle not found at " + packageZipPath);

        // get defined app bundles
        Page<string> appBundles = await GetClient().GetAppBundlesAsync();

        // check if app bundle is already define
        dynamic newAppVersion;
        string qualifiedAppBundleId = string.Format("{0}.{1}+{2}", _nickname, appBundleName, _alias);
        if (!appBundles.Data.Contains(qualifiedAppBundleId))
        {
            // create an appbundle (version 1)
            AppBundle appBundleSpec = new AppBundle()
            {
                Package = appBundleName,
                Engine = engineName,
                Id = appBundleName,
                Description = string.Format("Description for {0}", appBundleName),

            };
            newAppVersion = await GetClient().CreateAppBundleAsync(appBundleSpec);
            if (newAppVersion == null) throw new Exception("Cannot create new app");

            // create alias pointing to v1
            Alias aliasSpec = new Alias() { Id = _alias, Version = 1 };
            Alias newAlias = await GetClient().CreateAppBundleAliasAsync(appBundleName, aliasSpec);
        }
        else
        {
            // create new version
            AppBundle appBundleSpec = new AppBundle()
            {
                Engine = engineName,
                Description = appBundleName
            };
            newAppVersion = await GetClient().CreateAppBundleVersionAsync(appBundleName, appBundleSpec);
            if (newAppVersion == null) throw new Exception("Cannot create new version");

            // update alias pointing to v+1
            AliasPatch aliasSpec = new AliasPatch()
            {
                Version = newAppVersion.Version
            };
            Alias newAlias = await GetClient().ModifyAppBundleAliasAsync(appBundleName, _alias, aliasSpec);
        }

        // upload the zip with .bundle            
        using (var client = new HttpClient())
        {
            using (var formData = new MultipartFormDataContent())
            {
                foreach (var kv in newAppVersion.UploadParameters.FormData)
                {
                    if (kv.Value != null)
                    {
                        formData.Add(new StringContent(kv.Value), kv.Key);
                    }
                }
                using (var content = new StreamContent(new FileStream(packageZipPath, FileMode.Open)))
                {
                    formData.Add(content, "file");
                    using (var request = new HttpRequestMessage(HttpMethod.Post, newAppVersion.UploadParameters.EndpointURL) { Content = formData })
                    {
                        var response = await client.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
        }

        return new { AppBundle = qualifiedAppBundleId, Version = newAppVersion.Version };
    }

    public async Task<dynamic> CreateActivity([FromBody] JObject activitySpecs)
    {
        // basic input validation
        string zipFileName = activitySpecs["zipFileName"].Value<string>();
        string engineName = activitySpecs["engine"].Value<string>();

        // standard name for this sample
        string appBundleName = zipFileName + "AppBundle";
        string activityName = zipFileName + "Activity";

        // 
        Page<string> activities = await GetClient().GetActivitiesAsync();
        string qualifiedActivityId = string.Format("{0}.{1}+{2}", _nickname, activityName, _alias);
        if (!activities.Data.Contains(qualifiedActivityId))
        {
            // define the activity
            // ToDo: parametrize for different engines...
            dynamic engineAttributes = EngineAttributes(engineName);
            string commandLine = string.Format(engineAttributes.commandLine, appBundleName);
            Activity activitySpec = new Activity()
            {
                Id = activityName,
                Appbundles = new List<string>() { string.Format("{0}.{1}+{2}", _nickname, appBundleName, _alias) },
                CommandLine = new List<string>() { commandLine },
                Engine = engineName,
                Parameters = new Dictionary<string, Parameter>()
                {
                    { "inputFile", new Parameter() { Description = "input file", LocalName = "$(inputFile)", Ondemand = false, Required = true, Verb = Verb.Get, Zip = false } },
                    { "inputJson", new Parameter() { Description = "input json", LocalName = "params.json", Ondemand = false, Required = false, Verb = Verb.Get, Zip = false } },
                    { "outputFile", new Parameter() { Description = "output file", LocalName = "outputFile." + engineAttributes.extension, Ondemand = false, Required = true, Verb = Verb.Put, Zip = false } }
                },
                Settings = new Dictionary<string, ISetting>()
                {
                    { "script", new StringSetting(){ Value = engineAttributes.script } }
                }
            };
            Activity newActivity = await GetClient().CreateActivityAsync(activitySpec);

            // specify the alias for this Activity
            Alias aliasSpec = new Alias() { Id = _alias, Version = 1 };
            Alias newAlias = await GetClient().CreateActivityAliasAsync(activityName, aliasSpec);

            return new { Activity = qualifiedActivityId };
        }

        // as this activity points to a AppBundle "dev" alias (which points to the last version of the bundle),
        // there is no need to update it (for this sample), but this may be extended for different contexts
        return new { Activity = "Activity already defined" };
    }

    public async Task<List<string>> GetAvailableEngines()
    {
        List<string> allEngines = new List<string>();
        // define Engines API
        string paginationToken = null;
        while (true)
        {
            Page<string> engines = await GetClient().GetEnginesAsync(paginationToken);
            allEngines.AddRange(engines.Data);
            if (engines.PaginationToken == null)
                break;
            paginationToken = engines.PaginationToken;
        }
        allEngines.Sort();
        return allEngines; // return list of engines
    }

    public async Task<List<string>> GetDefinedActivities()
    {
        // filter list of 
        Page<string> activities = await GetClient().GetActivitiesAsync();
        List<string> definedActivities = new List<string>();
        foreach (string activity in activities.Data)
            if (activity.StartsWith(_nickname) && activity.IndexOf("$LATEST") == -1)
                definedActivities.Add(activity.Replace(_nickname + ".", String.Empty));

        return definedActivities;
    }

    public async Task<dynamic> StartWorkitem(IFormFile inputFile, string data, string contentRootPath)
    {
        // basic input validation
        JObject workItemData = JObject.Parse(data);
        string widthParam = workItemData["width"].Value<string>();
        string heigthParam = workItemData["height"].Value<string>();
        string activityName = string.Format("{0}.{1}", _nickname, workItemData["activityName"].Value<string>());

        // save the file on the server
        var fileSavePath = Path.Combine(contentRootPath, Path.GetFileName(inputFile.FileName));
        using (var stream = new FileStream(fileSavePath, FileMode.Create)) await inputFile.CopyToAsync(stream);

        // OAuth token
        dynamic auth = await GetInternalToken();

        // Upload inputFile
        string inputFileNameOSS = string.Format("{0}_input_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(inputFile.FileName));// avoid overriding
        // prepare workitem arguments
        // 1. input file
        var inputModel = await UploadModel(inputFileNameOSS, fileSavePath);
        XrefTreeArgument inputFileArgument = new XrefTreeArgument()
        {
            Url = inputModel.ObjectId,
            Headers = new Dictionary<string, string>(){
                { "Authorization", "Bearer " + auth.AccessToken} }
        };

        // 2. input json
        dynamic inputJson = new JObject();
        inputJson.Width = widthParam;
        inputJson.Height = heigthParam;
        XrefTreeArgument inputJsonArgument = new XrefTreeArgument()
        {
            Url = "data:application/json, " + ((JObject)inputJson).ToString(Formatting.None).Replace("\"", "'")
        };
        // 3. output file
        string outputFileNameOSS = string.Format("{0}_output_{1}", DateTime.Now.ToString("yyyyMMddhhmmss"), Path.GetFileName(inputFile.FileName)); // avoid overriding            
        var outputModel = await UploadModel(outputFileNameOSS, fileSavePath);
        XrefTreeArgument outputFileArgument = new XrefTreeArgument()
        {
            Url =outputModel.ObjectId,
            Headers = new Dictionary<string, string>()
            {
                { "Authorization", "Bearer " + auth.AccessToken}
            },
            Verb = Verb.Put
        };

        if (System.IO.File.Exists(fileSavePath))
        {
            System.IO.File.Delete(fileSavePath);
        }

        // prepare & submit workitem            
        WorkItem workItemSpec = new WorkItem()
        {
            ActivityId = activityName,
            Arguments = new Dictionary<string, IArgument>()
            {
                { "inputFile", inputFileArgument },
                { "inputJson",  inputJsonArgument },
                { "outputFile", outputFileArgument }
            }
        };
        WorkItemStatus workItemStatus = await GetClient().CreateWorkItemAsync(workItemSpec);

        return new { WorkItemId = workItemStatus.Id, FileName = outputFileNameOSS };
    }

    public async Task<WorkItemStatus> GetWorkitem(string id)
    {
        // filter list of 
        WorkItemStatus status = await GetClient().GetWorkitemStatusAsync(id);

        return status;
    }

    public async Task ClearAccount()
    {
        await GetClient().DeleteForgeAppAsync("me");
    }
}
