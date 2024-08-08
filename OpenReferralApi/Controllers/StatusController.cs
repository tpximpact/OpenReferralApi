using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Models;

namespace OpenReferralApi.Controllers;


[ApiController]
[Route("api/[Controller]")]
public class StatusController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(StatusResult), 200)]
    [ProducesResponseType(typeof(StatusResult), 500)]
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