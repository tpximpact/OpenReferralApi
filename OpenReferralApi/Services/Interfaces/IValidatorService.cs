using FluentResults;
using Json.Schema;
using OpenReferralApi.Models;

namespace OpenReferralApi.Services.Interfaces;

public interface IValidatorService
{
    public Task<Result<ValidationResponse>> ValidateService (string serviceUrl, string profile);
}