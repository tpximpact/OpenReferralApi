using OpenReferralApi.Models;

namespace OpenReferralApi.Services.Interfaces;

public interface ITestProfileService
{
    Task<(string, string)> SelectTestSchema(string serviceUrl, string? profileInput);
    Task<TestProfile?> ReadTestProfileFromFile(string testSchema);
}