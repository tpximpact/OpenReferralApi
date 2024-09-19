using System.Net;
using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class ValidateController : ControllerBase
{
    private readonly IValidatorService _validatorService;

    public ValidateController(IValidatorService validatorService)
    {
        _validatorService = validatorService;
    }

    /// <summary>
    /// This endpoint checks that a specific service directory follows the V3 ORUK standard 
    /// </summary>
    /// <param name="serviceUrl"></param>
    [HttpPost]
    public async Task<IActionResult> ValidateService([FromQuery]string serviceUrl)
    {
        var response = await _validatorService.ValidateService(serviceUrl);

        return response.IsSuccess ? Ok(response.Value) : BadRequest(response.Errors);
    }
    
}