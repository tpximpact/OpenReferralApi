using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.Extensions.Caching.Memory;
using OpenReferralApi.Services.Interfaces;
using System.Text.Json;
namespace OpenReferralApi.Services;

public class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    private readonly ILogger<RequestService> _logger;

    public RequestService(HttpClient httpClient, IMemoryCache cache, ILogger<RequestService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(20));
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
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request to {Url} timed out", url);
            return Result.Fail($"Request timed out after {_httpClient.Timeout.Seconds} seconds");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error occurred while parsing JSON response from {Url}", url);
            return Result.Fail("Failed to parse JSON response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while making API request to {Url}", url);
            return Result.Fail("Encountered an error whilst trying to make the API request");
        }
    }
}