using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class ValidateController : ControllerBase
{
    private readonly IValidatorService _validatorService;
    private readonly ILogger<ValidateController> _logger;

    public ValidateController(IValidatorService validatorService, ILogger<ValidateController> logger)
    {
        _validatorService = validatorService;
        _logger = logger;
    }

    /// <summary>
    /// This endpoint checks that a specific service directory follows the V3 ORUK standard 
    /// </summary>
    /// <param name="serviceUrl"></param>
    /// <param name="profile"></param>
    [HttpPost]
    public async Task<IResult> ValidateService([FromQuery] string serviceUrl, [FromQuery] string? profile)
    {
        try
        {
            var response = await _validatorService.ValidateService(serviceUrl, profile);

            return response.IsSuccess ? Results.Ok(response.Value) : Results.BadRequest(response.Errors);
        }
        catch (Exception e)
        {
            _logger.LogError("Error encountered during validation");
            _logger.LogError(e.Message);

            return Results.Problem(
                title: "Internal Server Error",
                detail: "A critical failure occurred during validation. If this error persists please contact ORUK",
                instance: HttpContext.Request.Path + HttpContext.Request.QueryString,
                statusCode: 500);
        }
    }
    
}