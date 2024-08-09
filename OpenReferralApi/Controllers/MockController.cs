using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using static System.IO.File;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class MockController : ControllerBase
{
    
    [HttpPost]
    [Route("validate")]
    public async Task<IActionResult> GetValidatorMock([FromQuery]string? serviceBaseUrl = null)
    {
        return await ReadJsonFile("Mocks/V1_ValidateResponse.json");
    }
    
    [HttpGet]
    [Route("dashboard")]
    public async Task<IActionResult> GetDashboardMock()
    {
        return await ReadJsonFile("Mocks/V1_DashboardResponse.json");
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