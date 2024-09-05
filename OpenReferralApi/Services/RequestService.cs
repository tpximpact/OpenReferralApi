using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.AspNetCore.WebUtilities;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;

    public RequestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<JsonNode>> GetApiResponse(string url, string endpoint)
    {
        throw new NotImplementedException();
    }

    public async Task<Result<JsonNode>> GetApiDetails(string url)
    {
        return await MakeRequest(url);
    }

    private async Task<Result<JsonNode>> MakeRequest(string endpoint)
    {
        var result = await _httpClient.GetAsync(endpoint);

        var resultString = await result.Content.ReadAsStringAsync();

        if (!result.IsSuccessStatusCode)
        {
            var error = new Error(result.ReasonPhrase, new Error(resultString));
            return Result.Fail(error);
        }

        var response = JsonNode.Parse(resultString);
        
        return Result.Ok(response)!;
    }

    private async Task<Result<JsonNode>> MakeRequest(string endpoint, IDictionary<string, string> parameters)
    {
        var url = QueryHelpers.AddQueryString(endpoint, parameters!);

        var result = await _httpClient.GetAsync(url);

        var resultString = await result.Content.ReadAsStringAsync();

        if (!result.IsSuccessStatusCode)
        {
            var error = new Error(result.ReasonPhrase, new Error(resultString));
            return Result.Fail(error);
        }

        var response = JsonNode.Parse(resultString);
        
        return Result.Ok(response)!;
    }
}