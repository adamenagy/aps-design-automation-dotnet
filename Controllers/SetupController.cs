using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

[ApiController]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly APS _aps;
    private readonly string _localBundlesFolder;

    public SetupController(IWebHostEnvironment env, APS aps)
    {
        _aps = aps;
        _localBundlesFolder = env.ContentRootPath + "/Bundles";
    }

    /// <summary>
    /// Start setup
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Setup([FromBody] JObject setupSpecs)
    {
        dynamic setup = await _aps.Setup(setupSpecs, _localBundlesFolder);

        return Ok(setup);
    }

    /// <summary>
    /// Return a list of available engines
    /// </summary>
    [HttpGet]
    [Route("engines")]
    public async Task<List<string>> GetAvailableEngines()
    {
        var engines =  await _aps.GetAvailableEngines();
        return engines;
    }

    /// <summary>
    /// Return a list of local app bundles
    /// </summary>
    [HttpGet]
    [Route("appbundles")]
    public string[] GetLocalBundles()
    {
        return Directory.GetFiles(_localBundlesFolder, "*.zip")
                        .Select(file => Path.GetFileNameWithoutExtension(file) ?? string.Empty)
                        .ToArray();
    }

    /// <summary>
    /// Return a list of available activities
    /// </summary>
    [HttpGet]
    [Route("activities")]
    public async Task<List<string>> GetDefinedActivities()
    {
        // get activities
        var activities = await _aps.GetDefinedActivities();
        return activities;
    }

    /// <summary>
    /// Clear the accounts (for e.g. debugging purposes)
    /// </summary>
    [HttpDelete]
    [Route("account")]
    public async Task<IActionResult> ClearAccount()
    {
        // clear account
        await _aps.ClearAccount();
        return Ok();
    }
}