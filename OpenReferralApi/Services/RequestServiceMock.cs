using System.Text.Json.Nodes;
using FluentResults;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class RequestServiceMock : IRequestService
{
    public async Task<Result<JsonNode>> GetApiResponse(string url, string endpoint)
    {
        var mockPath = "Mocks/V3.0-UK-";

        if (url.ToLower().Contains("fail"))
            mockPath += "Fail";
        else if (url.ToLower().Contains("warn"))
            mockPath += "Warn";
        else if (url.ToLower().Contains("test"))
            mockPath += "Test";
        else
            mockPath += "Default";
        
        return endpoint switch
        {
            "/services" => await ReadJsonFile($"{mockPath}/service_list.json"),
            "/services/{id}" => await ReadJsonFile($"{mockPath}/service_full.json"),
            "/taxonomies" => await ReadJsonFile($"{mockPath}/taxonomy_list.json"),
            "/taxonomies/{id}" => await ReadJsonFile($"{mockPath}/taxonomy.json"),
            "/taxonomy_terms" => await ReadJsonFile($"{mockPath}/taxonomy_term_list.json"),
            "/taxonomy_terms/{id}" => await ReadJsonFile($"{mockPath}/taxonomy_term.json"),
            "/service_at_locations" => await ReadJsonFile($"{mockPath}/service_at_location_list.json"),
            "/service_at_locations/{id}" => await ReadJsonFile($"{mockPath}/service_at_location_full.json"),
            _ => await ReadJsonFile($"{mockPath}/api_details.json")
        };
    }

    public async Task<Result<JsonNode>> GetApiResponse(string url, string endpoint, int perPage, int page)
    {
        return await GetApiResponse(url, endpoint);
    }

    public async Task<Result<JsonNode>> GetApiDetails(string url)
    {
        return await ReadJsonFile("Mocks/V3.0-UK-Default/V3_ApiDetails.json");
        // return await ReadJsonFile("Mocks/V3.0-UK-Default/V3_ApiDetails_ISSUES.json");
    }
    
    private async Task<Result<JsonNode>> ReadJsonFile(string filePath)
    {
        try
        {
            // Open the text file using a stream reader.
            using StreamReader reader = new(filePath);

            // Read the stream as a string.
            var fileContent = await reader.ReadToEndAsync();
            
            return Result.Ok(JsonNode.Parse(fileContent))!;
        }
        catch (IOException e)
        {
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
            return Result.Fail(e.Message);
        }
    } 
}