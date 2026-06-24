using Microsoft.AspNetCore.Mvc;

namespace OpenReferralApi.Controllers;

/// <summary>
/// Base controller for OpenAPI validation endpoints
/// Provides shared validation logic for different response format implementations
/// </summary>
internal abstract class BaseOpenApiController : ControllerBase
{
    private static readonly string[] OpenApiSchemaUrlError = ["OpenAPI schema URL must be provided or discoverable from baseUrl"];
    private static readonly string[] BaseUrlError = ["BaseUrl is required when testing endpoints"];
    /// <summary>
    /// Validates the incoming request for required fields
    /// </summary>
    /// <param name="request">The validation request to validate</param>
    /// <returns>A BadRequest ActionResult if validation fails, null if validation passes</returns>
    protected ActionResult? ValidateRequestAndReturnErrorIfInvalid(
        OpenApiValidationRequest request)
    {
        if (string.IsNullOrEmpty(request.OwnSchemaUrl) && string.IsNullOrEmpty(request.BaseUrl))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["request"] = OpenApiSchemaUrlError
            }));
        }

        if (string.IsNullOrEmpty(request.BaseUrl))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["baseUrl"] = BaseUrlError
            }));
        }

        return null;
    }
}
