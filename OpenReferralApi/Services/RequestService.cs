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
        try
        {
            var result = await _httpClient.GetAsync(endpoint);
        
            var resultString = await result.Content.ReadAsStringAsync();
        
            return result.IsSuccessStatusCode 
                ? JsonNode.Parse(resultString)!
                : Result.Fail(new Error(result.ReasonPhrase, new Error(resultString)));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result.Fail(e.Message);
        }
    }

    private async Task<Result<JsonNode>> MakeRequest(string endpoint, IDictionary<string, string> parameters)
    {
        try
        {
            var url = QueryHelpers.AddQueryString(endpoint, parameters!);

            var result = await _httpClient.GetAsync(url);

            var resultString = await result.Content.ReadAsStringAsync();

            return result.IsSuccessStatusCode
                ? JsonNode.Parse(resultString)!
                : Result.Fail(new Error(result.ReasonPhrase, new Error(resultString)));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result.Fail(e.Message);
        }
    }
}