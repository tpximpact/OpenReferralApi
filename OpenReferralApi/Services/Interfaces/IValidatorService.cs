using System.Text.Json.Nodes;
using FluentResults;
using Newtonsoft.Json.Schema;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Responses;

namespace OpenReferralApi.Services.Interfaces;

public interface IValidatorService
{
    public Task<Result<ValidationResponse>> ValidateService (string serviceUrl, string? profile);
    public Result<List<Issue>> ValidateResponseSchema(JsonNode response, JSchema schema);
    public Task<Result<List<string>>> FetchIds(string url);
    public Task<Result<List<Issue>>> ValidatePagination(TestCase testCase, string serviceUrl, JsonNode apiResponse);
    
}