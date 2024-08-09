using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Models;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class StatusController : ControllerBase
{
    
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
            Message = "Service running"
        };
        
        return Ok(statusResult);
    }
}