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
                "/services" => await ReadJsonFile("Mocks/ApiResponses/V3_ServicesList.json"),
                "/services/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_Service.json"),
                "/taxonomies" => await ReadJsonFile("Mocks/ApiResponses/V3_TaxonomyList.json"),
                "/taxonomies/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_Taxonomy.json"),
                "/taxonomy_terms" => await ReadJsonFile("Mocks/ApiResponses/V3_TaxonomyTermList.json"),
                "/taxonomy_terms/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_TaxonomyTerm.json"),
                "/service_at_locations" => await ReadJsonFile("Mocks/ApiResponses/V3_ServiceAtLocationList.json"),
                "/service_at_locations/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_ServiceAtLocation.json"),
                _ => await ReadJsonFile("Mocks/ApiResponses/V3_ApiDetails_ISSUES.json")
            };
        }
        
        return endpoint switch
        {
            "/services" => await ReadJsonFile("Mocks/ApiResponses/V3_ServicesList.json"),
            "/services/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_Service.json"),
            "/taxonomies" => await ReadJsonFile("Mocks/ApiResponses/V3_TaxonomyList.json"),
            "/taxonomies/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_Taxonomy.json"),
            "/taxonomy_terms" => await ReadJsonFile("Mocks/ApiResponses/V3_TaxonomyTermList.json"),
            "/taxonomy_terms/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_TaxonomyTerm.json"),
            "/service_at_locations" => await ReadJsonFile("Mocks/ApiResponses/V3_ServiceAtLocationList.json"),
            "/service_at_locations/{id}" => await ReadJsonFile("Mocks/ApiResponses/V3_ServiceAtLocation.json"),
            _ => await ReadJsonFile("Mocks/ApiResponses/V3_ApiDetails.json")
        };
    }

    public async Task<Result<JsonNode>> GetApiResponse(string url, string endpoint, int perPage, int page)
    {
        return await GetApiResponse(url, endpoint);
    }

    public async Task<Result<JsonNode>> GetApiDetails(string url)
    {
        return await ReadJsonFile("Mocks/ApiResponses/V3_ApiDetails.json");
        // return await ReadJsonFile("Mocks/ApiResponses/V3_ApiDetails_ISSUES.json");
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