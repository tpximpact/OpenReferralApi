using System.Text.Json.Nodes;
using FluentResults;

namespace OpenReferralApi.Services.Interfaces;

public interface IRequestService
{
    public Task<Result<JsonNode>> GetApiResponse(string url);
}