using System.Text.Json.Nodes;
using FluentResults;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Tests;

public class RequestServiceMock : IRequestService
{
    public async Task<Result<JsonNode>> GetApiResponse(string url)
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

        if (url.Contains('?'))
            url = url.Remove(url.IndexOf('?'));

        if (url.EndsWith("services"))
            return await ReadJsonFile($"{mockPath}/service_list.json");
        if (url.EndsWith("services/ac148810-d857-441c-9679-408f346de14b"))
            return await ReadJsonFile($"{mockPath}/service_full.json");
        if (url.EndsWith("taxonomies"))
            return await ReadJsonFile($"{mockPath}/taxonomy_list.json");
        if (url.EndsWith("taxonomies/5c4d79d7-cc55-470e-9f1f-8cad074e4892"))
            return await ReadJsonFile($"{mockPath}/taxonomy.json");
        if (url.EndsWith("taxonomy_terms"))
            return await ReadJsonFile($"{mockPath}/taxonomy_term_list.json");
        if (url.EndsWith("taxonomy_terms/3f7b145d-84af-42d7-8fae-eaca714b02b2"))
            return await ReadJsonFile($"{mockPath}/taxonomy_term.json");
        if (url.EndsWith("service_at_locations"))
            return await ReadJsonFile($"{mockPath}/service_at_location_list.json");
        if (url.EndsWith("service_at_locations/e94c9f38-1e8f-4564-91d4-d53501ab1765"))
            return await ReadJsonFile($"{mockPath}/service_at_location_full.json");
        return await ReadJsonFile($"{mockPath}/api_details.json");
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