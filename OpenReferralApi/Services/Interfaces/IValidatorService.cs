using FluentResults;
using Json.Schema;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Responses;

namespace OpenReferralApi.Services.Interfaces;

public interface IValidatorService
{
    public Task<Result<ValidationResponse>> ValidateService (string serviceUrl, string? profile);
}