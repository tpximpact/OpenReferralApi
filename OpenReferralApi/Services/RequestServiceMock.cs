using System.Text.Json.Nodes;
using FluentResults;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class RequestServiceMock : IRequestService
{
    public async Task<Result<JsonNode>> GetApiResponse(string url, string endpoint)
    {
        if (url.ToLower().Contains("issues"))
        {
            return endpoint switch
            {
                "/services" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_list.json"),
                "/services/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_full.json"),
                "/taxonomies" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy_list.json"),
                "/taxonomies/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy.json"),
                "/taxonomy_terms" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy_term_list.json"),
                "/taxonomy_terms/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy_term.json"),
                "/service_at_locations" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_at_location_list.json"),
                "/service_at_locations/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_at_location_full.json"),
                _ => await ReadJsonFile("Mocks/V3.0-UK-Default/api_details.json")
            };
        }
        
        return endpoint switch
        {
            "/services" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_list.json"),
            "/services/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_full.json"),
            "/taxonomies" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy_list.json"),
            "/taxonomies/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy.json"),
            "/taxonomy_terms" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy_term_list.json"),
            "/taxonomy_terms/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/taxonomy_term.json"),
            "/service_at_locations" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_at_location_list.json"),
            "/service_at_locations/{id}" => await ReadJsonFile("Mocks/V3.0-UK-Default/service_at_location_full.json"),
            _ => await ReadJsonFile("Mocks/V3.0-UK-Default/api_details.json")
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