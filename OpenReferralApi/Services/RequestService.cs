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

    public async Task<Result<JsonNode>> GetApiResponse(string url)
    {
        try
        {
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