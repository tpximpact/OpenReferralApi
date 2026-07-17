using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Services;
using OpenReferralApi.Logging;

namespace OpenReferralApi.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("openreferraluk")]
[Route("api/openapi")] // Legacy route for backward compatibility, will be removed in future versions (once openreferraluk website is updated to point to new route)
[Produces("application/json")]
[EnableRateLimiting("fixed")]
internal sealed class OpenReferralUkController(
    IOpenApiValidationService openApiValidationService,
    ILogger<OpenReferralUkController> logger,
    IOpenReferralUKValidationResponseMapper mapper) : BaseOpenApiController
{
    private readonly IOpenApiValidationService _openApiValidationService = openApiValidationService;
    private readonly ILogger<OpenReferralUkController> _logger = logger;
    private readonly IOpenReferralUKValidationResponseMapper _mapper = mapper;

    /// <summary>
    /// Validates an OpenAPI specification and tests all defined endpoints, returning Open Referral UK formatted results
    /// </summary>
    /// <param name="request">The validation request containing OpenAPI URL and base URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation results mapped to Open Referral UK format</returns>
    /// <response code="200">Validation completed successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="429">Rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(OpenReferralUKValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OpenReferralUKValidationResponse>> ValidateAsync(
        [FromBody] OpenApiValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var sanitizedBaseUrl = TextSanitizer.SanitizeUrlForLogging(request.BaseUrl ?? string.Empty);
            OpenReferralUkControllerLog.ReceivedValidationRequest(_logger, sanitizedBaseUrl);
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
            OpenReferralUkControllerLog.ValidationCompleted(_logger, sanitizedBaseUrl);
        }

        var mappedResult = _mapper.MapToOpenReferralUKValidationResponse(result);
        return Ok(mappedResult);
    }
}
