using FluentResults;
using OpenReferralApi.Models;

namespace OpenReferralApi.Services.Interfaces;

public interface ITestProfileService
{
    public Task<(string, string)> SelectTestSchema(string serviceUrl, string? profileInput);
    public Task<Result<TestProfile>> ReadTestProfileFromFile(string testSchema);
}