using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Core.Models;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OpenApiController : ControllerBase
{
    private readonly IOpenApiValidationService _openApiValidationService;
    private readonly ILogger<OpenApiController> _logger;
    private readonly IOpenApiDiscoveryService _discoveryService;
    private readonly IOpenApiToValidationResponseMapper _mapper;

    public OpenApiController(
        IOpenApiValidationService openApiValidationService, 
        ILogger<OpenApiController> logger, 
        IOpenApiDiscoveryService discoveryService,
        IOpenApiToValidationResponseMapper mapper)
    {
        _openApiValidationService = openApiValidationService;
        _logger = logger;
        _discoveryService = discoveryService;
        _mapper = mapper;
    }

    /// <summary>
    /// Validates an OpenAPI specification and optionally tests all defined endpoints
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> ValidateOpenApiSpecificationAsync(
        [FromBody] OpenApiValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received OpenAPI validation request");

            // If OpenApiSchemaUrl not provided, try to discover it from BaseUrl response's `openapi_url`.
            if (string.IsNullOrEmpty(request.OpenApiSchemaUrl))
            {
                if (!string.IsNullOrEmpty(request.BaseUrl))
                {
                    var discovered = await _discoveryService.DiscoverOpenApiUrlAsync(request.BaseUrl, cancellationToken);
                    if (!string.IsNullOrEmpty(discovered))
                    {
                        request.OpenApiSchemaUrl = discovered;
                    }
                }
                else
                {
                    return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["request"] = new[] { "OpenApiSchemaUrl must be provided" }
                    }));
                }
            }

            if (string.IsNullOrEmpty(request.BaseUrl))
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["baseUrl"] = new[] { "BaseUrl is required when testing endpoints" }
                }));
            }

            var result = await _openApiValidationService.ValidateOpenApiSpecificationAsync(request, cancellationToken);
            
            // Return raw result or mapped to ValidationResponse format based on option
            if (request.Options?.ReturnRawResult == true)
            {
                return Ok(result);
            }
            else
            {
                var mappedResult = _mapper.MapToValidationResponse(result);
                return Ok(mappedResult);
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid OpenAPI validation request");
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["request"] = new[] { ex.Message }
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OpenAPI validation");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "An error occurred during OpenAPI validation",
                message = ex.Message
            });
        }
    }
}
