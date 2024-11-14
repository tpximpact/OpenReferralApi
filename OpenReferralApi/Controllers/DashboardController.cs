using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.AspNetCore.Mvc;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Controllers;

[ApiController]
[Route("api/[Controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Returns data about the known HSDS-UK services and the details needed for the data to be understood & displayed 
    /// </summary>
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetDashboardServices()
    {
        var result =  await _dashboardService.GetServices();

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
    }
    
    /// <summary>
    /// Returns detailed data about a single HSDS-UK service and the details needed for the data to be understood & displayed 
    /// </summary>
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetDashboardServiceDetails([FromRoute]string id)
    {
        if (int.TryParse(id, out var idInteger) && idInteger < 50)
            return await ReadJsonFile("Mocks/V3.0-UK-Default/dashboard_service_details_response.json");
        
        var result =  await _dashboardService.GetServiceById(id);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Errors);
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