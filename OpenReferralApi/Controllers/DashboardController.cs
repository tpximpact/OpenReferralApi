using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Requests;
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
    /// Returns data about the known HSDS-UK services and the details needed for the data to be understood &amp; displayed.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a list of dashboard services if successful,
    /// or <c>400 Bad Request</c> with error details if the operation fails.
    /// </returns>
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetDashboardServices()
    {
        var result = await _dashboardService.GetServices();

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
    }

    /// <summary>
    /// Returns detailed data about a single HSDS-UK service and the details needed for the data to be understood &amp; displayed.
    /// </summary>
    /// <param name="id">The unique identifier of the dashboard service.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the service details if successful,
    /// or <c>400 Bad Request</c> with error details if the operation fails.
    /// </returns>
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetDashboardServiceDetails([FromRoute] string id)
    {
        var result = await _dashboardService.GetServiceById(id);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
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

    /// <summary>
    /// Validates a dashboard service by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the dashboard service to validate.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the validation result for the specified service if successful,
    /// or <c>400 Bad Request</c> with error details if validation fails.
    /// </returns>
    [HttpGet]
    [Route("validate/{id}")]
    public async Task<IActionResult> ValidateDashboardService([FromRoute] string id)
    {
        var result = await _dashboardService.ValidateDashboardService(id);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
    }

    /// <summary>
    /// Submits a service to the dashboard.
    /// </summary>
    /// <param name="submission">The submission details for the dashboard service.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> with <c>202 Accepted</c> and the submission result if successful,
    /// or <c>400 Bad Request</c> with error details if the submission fails.
    /// </returns>
    [HttpPost]
    [Route("submit")]
    public async Task<IActionResult> SubmitDashboardService([FromBody] DashboardSubmissionRequest submission)
    {
        var submissionResult = await _dashboardService.SubmitService(submission);

        if (submissionResult.IsSuccess)
            return Accepted(submissionResult.Value);

        return BadRequest(submissionResult.Value);
    }
}