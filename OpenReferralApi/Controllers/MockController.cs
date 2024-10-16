using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class MockController : ControllerBase
{
    private const string MockPath = "Mocks/V3.0-UK-";
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("")]
    [Route("fail")]
    [Route("warn")]
    public async Task<IActionResult> GetServiceMetadata()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/api_details.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/api_details.json");
        return await ReadJsonFile($"{MockPath}default/api_details.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("services")]
    [Route("fail/services")]
    [Route("warn/services")]
    public async Task<IActionResult> GetServices()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/service_list.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/service_list.json");
        return await ReadJsonFile($"{MockPath}default/service_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("services/{id}")]
    [Route("fail/services/{id}")]
    [Route("warn/services/{id}")]
    public async Task<IActionResult> GetServicesById()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/service_full.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/service_full.json");
        return await ReadJsonFile($"{MockPath}default/service_full.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomies")]
    [Route("fail/taxonomies")]
    [Route("warn/taxonomies")]
    public async Task<IActionResult> GetTaxonomies()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/taxonomy_list.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/taxonomy_list.json");
        return await ReadJsonFile($"{MockPath}default/taxonomy_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomies/{id}")]
    [Route("fail/taxonomies/{id}")]
    [Route("warn/taxonomies/{id}")]
    public async Task<IActionResult> GetTaxonomiesById()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/taxonomy.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/taxonomy.json");
        return await ReadJsonFile($"{MockPath}default/taxonomy.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomy_terms")]
    [Route("fail/taxonomy_terms")]
    [Route("warn/taxonomy_terms")]
    public async Task<IActionResult> GetTaxonomyTerms()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/taxonomy_term_list.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/taxonomy_term_list.json");
        return await ReadJsonFile($"{MockPath}default/taxonomy_term_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("taxonomy_terms/{id}")]
    [Route("fail/taxonomy_terms/{id}")]
    [Route("warn/taxonomy_terms/{id}")]
    public async Task<IActionResult> GetTaxonomyTermsById()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/taxonomy_term.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/taxonomy_term.json");
        return await ReadJsonFile($"{MockPath}default/taxonomy_term.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("service_at_locations")]
    [Route("fail/service_at_locations")]
    [Route("warn/service_at_locations")]
    public async Task<IActionResult> GetServiceAtLocations()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/service_at_location_list.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/service_at_location_list.json");
        return await ReadJsonFile($"{MockPath}default/service_at_location_list.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the expected response from the V3 GET /services/{id} endpoint  
    /// </summary>
    [HttpGet]
    [Route("service_at_locations/{id}")]
    [Route("fail/service_at_locations/{id}")]
    [Route("warn/service_at_locations/{id}")]
    public async Task<IActionResult> GetServiceAtLocationsById()
    {
        var requestPath = Request.Path.ToString();
        if (requestPath.Contains("fail", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}fail/service_at_location_full.json");
        if (requestPath.Contains("warn", StringComparison.CurrentCultureIgnoreCase))
            return await ReadJsonFile($"{MockPath}warn/service_at_location_full.json");
        return await ReadJsonFile($"{MockPath}default/service_at_location_full.json");
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