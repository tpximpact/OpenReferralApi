using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class MockController : ControllerBase
{
    private const string MockPath = "Mocks/V3.0-UK-Default";
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetServiceMetadata()
    {
        return await ReadJsonFile($"{MockPath}/api_details.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("services")]
    public async Task<IActionResult> GetServices()
    {
        return await ReadJsonFile($"{MockPath}/service_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("services/{id}")]
    public async Task<IActionResult> GetServicesById()
    {
        return await ReadJsonFile($"{MockPath}/service_full.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomies")]
    public async Task<IActionResult> GetTaxonomies()
    {
        return await ReadJsonFile($"{MockPath}/taxonomy_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomies/{id}")]
    public async Task<IActionResult> GetTaxonomiesById()
    {
        return await ReadJsonFile($"{MockPath}/taxonomy.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomy_terms")]
    public async Task<IActionResult> GetTaxonomyTerms()
    {
        return await ReadJsonFile($"{MockPath}/taxonomy_term_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomy_terms/{id}")]
    public async Task<IActionResult> GetTaxonomyTermsById()
    {
        return await ReadJsonFile($"{MockPath}/taxonomy_term.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("service_at_locations")]
    public async Task<IActionResult> GetServiceAtLocations()
    {
        return await ReadJsonFile($"{MockPath}/service_at_location_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("service_at_locations/{id}")]
    public async Task<IActionResult> GetServiceAtLocationsById()
    {
        return await ReadJsonFile($"{MockPath}/service_at_location_full.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the V1 `/validate` response 
    /// </summary>
    /// <param name="serviceUrl"></param>
    [HttpPost]
    [Route("v1/validate")]
    public async Task<IActionResult> GetV1ValidatorMock([FromQuery]string? serviceUrl = null)
    {
        return await ReadJsonFile("Mocks/V1.0-UK-Default/V1_ValidateResponse.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the V1 `/dashboard` response
    /// </summary>
    [HttpGet]
    [Route("v1/dashboard")]
    [Route("dashboard")]
    public async Task<IActionResult> GetDashboardMock()
    {
        return await ReadJsonFile("Mocks/V1.0-UK-Default/V1_DashboardResponse.json");
    }

    private async Task<IActionResult> ReadJsonFile(string filePath)
    {
        try
        {
            // Open the text file using a stream reader.
            using StreamReader reader = new(filePath);

            // Read the stream as a string.
            var mock = await reader.ReadToEndAsync();

            var mockResponse = JsonNode.Parse(mock);

            return Ok(mockResponse);
        }
        catch (IOException e)
        {
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
        }

        return StatusCode(500, "Sorry, something went wrong when trying to return the mock");
    } 
}