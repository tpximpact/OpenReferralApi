using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class DashboardController : ControllerBase
{
    /// <summary>
    /// Returns data about the known HSDS-UK services and the details needed for the data to be understood & displayed 
    /// </summary>
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetDashboard()
    {
        return await ReadJsonFile("Mocks/V3.0-UK-Default/dashboard_response.json");
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

        return StatusCode(500, "Sorry, something went wrong when trying to read the dashboard data");
    } 
}