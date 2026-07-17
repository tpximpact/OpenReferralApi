using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Services;
using OpenReferralApi.Logging;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("openreferral")]
[Produces("application/json")]
[EnableRateLimiting("fixed")]
internal sealed class OpenReferralController(
    IOpenApiValidationService openApiValidationService,
    ILogger<OpenReferralController> logger) : BaseOpenApiController
{
    private readonly IOpenApiValidationService _openApiValidationService = openApiValidationService;
    private readonly ILogger<OpenReferralController> _logger = logger;

    /// <summary>
    /// Validates an OpenAPI specification and tests all defined endpoints, returning raw results
    /// </summary>
    /// <param name="request">The validation request containing OpenAPI URL and base URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Raw validation results</returns>
    /// <response code="200">Validation completed successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="429">Rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(OpenApiValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OpenApiValidationResult>> ValidateAsync(
        [FromBody] OpenApiValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var sanitizedBaseUrl = TextSanitizer.SanitizeUrlForLogging(request.BaseUrl ?? string.Empty);
            OpenApiControllerLog.ReceivedValidationRequest(_logger, sanitizedBaseUrl);
        }

        var validationError = ValidateRequestAndReturnErrorIfInvalid(request);
        if (validationError != null)
        {
            return validationError;
        }

        var result = await _openApiValidationService.ValidateOpenApiSpecificationAsync(request, cancellationToken).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var sanitizedBaseUrl = TextSanitizer.SanitizeUrlForLogging(request.BaseUrl ?? string.Empty);
            OpenApiControllerLog.ValidationCompleted(_logger, sanitizedBaseUrl);
        }

        return Ok(result);
    }
}
