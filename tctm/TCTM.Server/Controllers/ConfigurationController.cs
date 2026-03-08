using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace TCTM.Server.Controllers;

[ApiController]
[Route("api/configuration")]
public class ConfigurationController(IOptions<TctmConfiguration> options) : ControllerBase
{
    /// <summary>GET /api/configuration — Returns public application configuration.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        var config = options.Value;
        return Ok(new
        {
            config.ApplicationUrl
        });
    }
}
