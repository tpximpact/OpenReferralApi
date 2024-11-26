using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenReferralApi.Models;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class StatusController : ControllerBase
{
    private readonly string _dbName;

    public StatusController(IOptions<DatabaseSettings> databaseSettings)
    {
        _dbName = databaseSettings.Value.DatabaseName;
    }
    
    /// <summary>
    /// A status endpoint that can be used to check if the API is running and healthy
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(StatusResult), 200)]
    [ProducesResponseType(typeof(StatusResult), 500)]
    [Description("A status endpoint that can be used to check if the API is running and healthy")]
    public IActionResult GetStatus()
    {
        var statusResult = new StatusResult()
        {
            Healthy = true,
            Message = $"Service running - {_dbName}"
        };
        
        return Ok(statusResult);
    }
}