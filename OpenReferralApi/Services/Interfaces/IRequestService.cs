using System.Text.Json.Nodes;
using FluentResults;

namespace OpenReferralApi.Services.Interfaces;

public interface IRequestService
{
    public Task<Result<JsonNode>> GetApiResponse(string url, string endpoint);
    public Task<Result<JsonNode>> GetApiResponse(string url, string endpoint, int perPage, int page);
    public Task<Result<JsonNode>> GetApiDetails(string url);
}