using Autodesk.Forge.DesignAutomation.Model;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class WorkitemsController : ControllerBase
{
    private readonly APS _aps;
    private readonly string _contentRootPath;

    public WorkitemsController(IWebHostEnvironment env, APS aps)
    {
        _aps = aps;
        _contentRootPath = env.ContentRootPath;
    }

    /// <summary>
    /// Start workitem
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> StartWorkitem([FromForm] StartWorkitemInput input)
    {
        dynamic workitem = await _aps.StartWorkitem(input.inputFile, input.data, _contentRootPath);

        return Ok(workitem);
    }

    /// <summary>
    /// Get workitem
    /// </summary>
    [HttpGet]
    [Route("{id}")]
    public async Task<WorkItemStatus> GetWorkitem(string id)
    {
        // filter list of 
        WorkItemStatus status = await _aps.GetWorkitem(id);

        return status;
    }

    /// <summary>
    /// Get download url
    /// </summary>
    [HttpGet]
    [Route("files/{name}/url")]
    public async Task<dynamic> GetDownloadUrl(string name)
    {
        // filter list of 
        var url = await _aps.GetDownloadUrl(name);

        return new { Url = url };
    }

    /// <summary>
    /// Input for StartWorkitem
    /// </summary>
    public class StartWorkitemInput
    {
        public IFormFile inputFile { get; set; }
        public string data { get; set; }
    }
}