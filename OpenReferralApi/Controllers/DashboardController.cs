using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Models;
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
    /// Returns data about the known HSDS-UK services and the details needed for the data to be understood & displayed
    /// </summary>
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetDashboardServices()
    {
        var result =  await _dashboardService.GetServices();

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
    }
    
    /// <summary>
    /// Returns detailed data about a single HSDS-UK service and the details needed for the data to be understood & displayed
    /// </summary>
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetDashboardServiceDetails([FromRoute]string id)
    {
        var result =  await _dashboardService.GetServiceById(id);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
    }

    /// <summary>
    /// Runs validation testing against all the services on the dashboard
    /// </summary>
    [HttpGet]
    [Route("validate")]
    public async Task<IActionResult> ValidateDashboardServices()
    {
        var result = await _dashboardService.ValidateDashboardServices();
        
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
    }

    /// <summary>
    /// Submits a service to the dashboard
    /// </summary>
    [HttpPost]
    [Route("submit")]
    public async Task<IActionResult> SubmitDashboardService([FromBody] DashboardSubmission submission)
    {
        var submissionResult = await _dashboardService.SubmitService(submission);

        if (submissionResult.IsSuccess)
            return Accepted(submissionResult.Value);
        
        return BadRequest(submissionResult.Value);
    } 
}