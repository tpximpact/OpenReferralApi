using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Runs validation testing against all the services on the dashboard.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the validation results for all services if successful,
    /// or <c>400 Bad Request</c> with error details if validation fails.
    /// </returns>
    [HttpGet]
    [Route("validate")]
    public async Task<IActionResult> ValidateDashboardServices()
    {
        var result = await _dashboardService.ValidateDashboardServices();

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
    }

}