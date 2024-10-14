using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using static System.IO.File;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class MockController : ControllerBase
{
    /// <summary>
    /// A MOCK endpoint that returns an example of the V1 `/validate` response 
    /// </summary>
    /// <param name="serviceUrl"></param>
    [HttpPost]
    [Route("validate")]
    public async Task<IActionResult> GetValidatorMock([FromQuery]string? serviceUrl = null)
    {
        return await ReadJsonFile("Mocks/V1.0-UK-Default/V1_ValidateResponse.json");
    }
    
    /// <summary>
    /// A MOCK endpoint that returns an example of the V1 `/dashboard` response
    /// </summary>
    [HttpGet]
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