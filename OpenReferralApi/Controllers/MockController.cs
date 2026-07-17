using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using OpenReferralApi.Core.Logging;
using OpenReferralApi.Models;

namespace OpenReferralApi.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/[Controller]")]
internal sealed class MockController(ILogger<MockController> logger) : ControllerBase
{
    private const string MockPath = "Mocks/V3.0-UK-";
    private readonly ILogger<MockController> _logger = logger;

    /// <summary>
    /// Resolves the appropriate mock file path based on the request path (fail/warn/default)
    /// </summary>
    private string ResolveMockPath(string fileName)
    {
        var requestPath = Request.Path.Value ?? string.Empty;
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return $"{MockPath}Fail/{fileName}";
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return $"{MockPath}Warn/{fileName}";
        return $"{MockPath}Default/{fileName}";
    }

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 API details GET / endpoint  
    /// </summary>
    [HttpGet]
    [Route("")]
    [Route("fail")]
    [Route("warn")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetServiceMetadata() => Task.FromResult(ReadJsonFile(ResolveMockPath("api_details.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services endpoint  
    /// </summary>
    [HttpGet]
    [Route("services")]
    [Route("fail/services")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [Route("warn/services")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetServices() => Task.FromResult(ReadJsonFile(ResolveMockPath("service_list.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint.
    /// As this is a mock the {id} value does not need to be valid 
    /// </summary>
    [HttpGet]
    [Route("services/{id}")]
    [Route("fail/services/{id}")]
    [Route("warn/services/{id}")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetServicesById() => Task.FromResult(ReadJsonFile(ResolveMockPath("service_full.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /taxonomies endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomies")]
    [Route("fail/taxonomies")]
    [Route("warn/taxonomies")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetTaxonomies() => Task.FromResult(ReadJsonFile(ResolveMockPath("taxonomy_list.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /taxonomies/{id} endpoint. 
    /// As this is a mock the {id} value does not need to be valid
    /// </summary>
    [HttpGet]
    [Route("taxonomies/{id}")]
    [Route("fail/taxonomies/{id}")]
    [Route("warn/taxonomies/{id}")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetTaxonomiesById() => Task.FromResult(ReadJsonFile(ResolveMockPath("taxonomy.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /taxonomy_terms endpoint
    /// </summary>
    [HttpGet]
    [Route("taxonomy_terms")]
    [Route("fail/taxonomy_terms")]
    [Route("warn/taxonomy_terms")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetTaxonomyTerms() => Task.FromResult(ReadJsonFile(ResolveMockPath("taxonomy_term_list.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /taxonomy_terms/{id} endpoint.  
    /// As this is a mock the {id} value does not need to be valid
    /// </summary>
    [HttpGet]
    [Route("taxonomy_terms/{id}")]
    [Route("fail/taxonomy_terms/{id}")]
    [Route("warn/taxonomy_terms/{id}")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetTaxonomyTermsById() => Task.FromResult(ReadJsonFile(ResolveMockPath("taxonomy_term.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /service_at_locations endpoint
    /// </summary>
    [HttpGet]
    [Route("service_at_locations")]
    [Route("fail/service_at_locations")]
    [Route("warn/service_at_locations")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetServiceAtLocations() => Task.FromResult(ReadJsonFile(ResolveMockPath("service_at_location_list.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /service_at_locations/{id} endpoint.  
    /// As this is a mock the {id} value does not need to be valid
    /// </summary>
    [HttpGet]
    [Route("service_at_locations/{id}")]
    [Route("fail/service_at_locations/{id}")]
    [Route("warn/service_at_locations/{id}")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetServiceAtLocationsById() => Task.FromResult(ReadJsonFile(ResolveMockPath("service_at_location_full.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the V1 `/validate` response 
    /// </summary>
    [HttpPost]
    [Route("v1/validate")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetV1ValidatorMock() => Task.FromResult(ReadJsonFile("Mocks/V1.0-UK-Default/V1_ValidateResponse.json"));

    /// <summary>
    /// A MOCK endpoint that returns an example of the V1 `/dashboard` response
    /// </summary>
    [HttpGet]
    [Route("v1/dashboard")]
    [Route("dashboard")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetDashboardMock() => Task.FromResult(ReadJsonFile("Mocks/V1.0-UK-Default/V1_DashboardResponse.json"));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /organizations endpoint  
    /// </summary>
    [HttpGet]
    [Route("organizations")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetOrganizations() => Task.FromResult(ReadJsonFile(ResolveMockPath("organization_list.json")));

    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /organizations/{id} endpoint. 
    /// As this is a mock the {id} value does not need to be valid
    /// </summary>
    [HttpGet]
    [Route("organizations/{id}")]
    [OutputCache(PolicyName = "MockEndpoints")]
    [ProducesResponseType(typeof(JsonNode), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetOrganizationsById() => Task.FromResult(ReadJsonFile(ResolveMockPath("organization.json")));

    private IActionResult ReadJsonFile(string filePath)
    {
        try
        {
            _logger.ReadingMockJsonFile(filePath);

            var absolutePath = System.IO.Path.GetFullPath(filePath);
            if (!System.IO.File.Exists(absolutePath))
            {
                _logger.MockFileNotFound(new FileNotFoundException("Mock file not found", absolutePath), absolutePath);
                return NotFound(new ApiErrorResponse
                {
                    Error = "Mock file not found",
                    File = filePath
                });
            }

            return PhysicalFile(absolutePath, "application/json");
        }
        catch (IOException ex)
        {
            _logger.ErrorReadingMockFile(ex, filePath);
            return StatusCode(500, new ApiErrorResponse
            {
                Error = "Error reading mock file",
                Message = ex.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.UnexpectedErrorReadingMockFile(ex, filePath);
            return StatusCode(500, new ApiErrorResponse
            {
                Error = "Unauthorized access to mock file",
                Message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.UnexpectedErrorReadingMockFile(ex, filePath);
            return StatusCode(500, new ApiErrorResponse
            {
                Error = "Invalid path for mock file",
                Message = ex.Message
            });
        }
    }
}
