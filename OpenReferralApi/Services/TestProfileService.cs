using FluentResults;
using Newtonsoft.Json;
using OpenReferralApi.Constants;
using OpenReferralApi.Models;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class TestProfileService : ITestProfileService
{
    private readonly ILogger<TestProfileService> _logger;
    private readonly IRequestService _requestService;


    public TestProfileService(ILogger<TestProfileService> logger, IRequestService requestService)
    {
        _logger = logger;
        _requestService = requestService;
    }

    public async Task<(string, string)> SelectTestSchema(string serviceUrl, string? profileInput)
    {
        const string defaultReason = "Could not read standard version from '/' endpoint defaulting to HSDS-UK-3.0";

        try
        {
            if (!string.IsNullOrEmpty(profileInput))
            {
                return profileInput switch
                {
                    HSDSUKVersions.V1 => (HSDSUKVersions.V1, "Standard version HSDS-UK-1.0 read from profile parameter"),
                    HSDSUKVersions.V3 => (HSDSUKVersions.V3, "Standard version HSDS-UK-3.0 read from profile parameter"),
                    _ => (HSDSUKVersions.V3, "Could not read standard version from profile parameter defaulting to HSDS-UK-3.0")
                };
            }

            var apiResult = await _requestService.GetApiResponse(serviceUrl);
            if (apiResult.IsFailed) 
                return (HSDSUKVersions.V1 , "Could not read response from '/' endpoint defaulting to HSDS-UK-1.0");
            
            return apiResult.Value["version"]!.ToString() switch
            {
                HSDSUKVersions.V1  => (HSDSUKVersions.V1 , "Standard version HSDS-UK-1.0 read from '/' endpoint"),
                HSDSUKVersions.V3 => (HSDSUKVersions.V3, "Standard version HSDS-UK-3.0 read from '/' endpoint"),
                _ => (HSDSUKVersions.V3, defaultReason)
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Error encountered when selecting the test schema");
            _logger.LogError(e.Message);
        }

        return (HSDSUKVersions.V3, defaultReason);
    }

    public async Task<Result<TestProfile>> ReadTestProfileFromFile(string testSchema)
    {
        try
        {
            var filePath = $"TestProfiles/{testSchema}.json";
            
            // Open the text file using a stream reader.
            using StreamReader reader = new(filePath);

            // Read the stream as a string.
            var fileContent = await reader.ReadToEndAsync();
            var testProfile = JsonConvert.DeserializeObject<TestProfile>(fileContent);
            
            return Result.Ok(testProfile)!;
        }
        catch (IOException e)
        {
            
            _logger.LogError("Error encountered when reading from file");
            _logger.LogError(e.Message);
            return Result.Fail(e.Message);
        }
    }
}