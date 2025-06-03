using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.Extensions.Caching.Memory;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public RequestService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
        _cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(90));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "oruk");
    }

    public async Task<Result<JsonNode>> GetApiResponse(string url)
    {
        try
        {
            if (_cache.TryGetValue(url, out JsonNode? cacheData))
                return cacheData!;

            var result = await _httpClient.GetAsync(url);
            var resultString = await result.Content.ReadAsStringAsync();

            if (!result.IsSuccessStatusCode)
                return Result.Fail(new Error(result.ReasonPhrase, new Error(resultString)));

            var responseData = JsonNode.Parse(resultString);

            _cache.Set(url, responseData, _cacheOptions);

            return responseData!;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result.Fail(e.Message);
        }
    }
}