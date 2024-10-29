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
        var requestUrl = url + endpoint;

        return await MakeRequest(requestUrl);
    }

    public async Task<Result<JsonNode>> GetApiResponse(string url, string endpoint, int perPage, int page)
    {
        var requestUrl = url + endpoint;
        var parameters = new Dictionary<string, string>
        {
            { "perPage", perPage.ToString() },
            { "page", page.ToString() }
        };

        return await MakeRequest(requestUrl, parameters);
    }

    public async Task<Result<JsonNode>> GetApiDetails(string url)
    {
        return await MakeRequest(url);
    }

    private async Task<Result<JsonNode>> MakeRequest(string endpoint)
    {
        var result = await _httpClient.GetAsync(endpoint);

        var resultString = await result.Content.ReadAsStringAsync();

        return result.IsSuccessStatusCode 
            ? Result.Fail(new Error(result.ReasonPhrase, new Error(resultString))) 
            : Result.Try(() => JsonNode.Parse(resultString)!);
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