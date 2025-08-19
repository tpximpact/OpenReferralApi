using System.Text.Json.Nodes;
using FluentResults;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using OpenReferralApi.Constants;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Responses;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class ValidatorService : IValidatorService
{
    private readonly ILogger<ValidatorService> _logger;
    private readonly IRequestService _requestService;
    private readonly ITestProfileService _testProfileService;
    private readonly IPaginationTestingService _paginationTestingService;

    public ValidatorService(ILogger<ValidatorService> logger, IRequestService requestService,
        ITestProfileService testProfileService, IPaginationTestingService paginationTestingService)
    {
        _logger = logger;
        _requestService = requestService;
        _testProfileService = testProfileService;
        _paginationTestingService = paginationTestingService;
    }

    public async Task<Result<ValidationResponse>> ValidateService(string serviceUrl, string? profile)
    {
        serviceUrl = serviceUrl.TrimEnd('/');

        if (serviceUrl == null || string.IsNullOrWhiteSpace(serviceUrl))
        {
            _logger.LogError("Service URL cannot be null or empty");
            return Result.Fail("Service URL cannot be null or empty");
        }

        var isUrlValid = Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        if (!isUrlValid)
        {
            _logger.LogError("Invalid service URL format: {Url}", serviceUrl);
            return Result.Fail("Invalid URL provided");
        }

        var (testSchema, schemaReason) = await _testProfileService.SelectTestSchema(serviceUrl, profile);
        var testProfile = await _testProfileService.ReadTestProfileFromFile(testSchema);

        if (testProfile == null)
        {
            return Result.Fail("Failed to read test profile");
        }

        var testProfileTasks = testProfile.TestGroups.Select(testGroup =>
            TestTestGroup(serviceUrl, testSchema, testGroup));
        var testProfileResults = await Task.WhenAll(testProfileTasks);

        return Result.Ok(new ValidationResponse
        {
            Service = new ServiceResponse
            {
                Url = serviceUrl,
                IsValid = testProfileResults
                    .Where(tg => tg.Required)
                    .All(tg => tg.Success),
                Profile = testProfile.Profile,
                ProfileReason = schemaReason
            },
            TestSuites = [.. testProfileResults],
        });
    }

    private async Task<TestGroup> TestTestGroup(string serviceUrl, string testSchema, TestCaseGroup testGroup)
    {

        var testGroupTasks = testGroup.Tests.Select(testCase => ValidateTestCase(testCase, serviceUrl, testSchema));
        var testGroupResults = await Task.WhenAll(testGroupTasks);

        var testGroupResultTests = testGroupResults.Select(r => r.Value);

        return new TestGroup
        {
            Name = testGroup.Name,
            Description = testGroup.Description,
            Required = testGroup.Required,
            MessageLevel = testGroup.MessageLevel,
            Success = testGroupResultTests.All(t => t.Success),
            Tests = [.. testGroupResultTests]
        };
    }

    private async Task<Result<Test>> ValidateTestCase(TestCase testCase, string serviceUrl, string schemaVersion)
    {
        var test = new Test
        {
            Name = testCase.Name,
            Description = testCase.Description,
            Endpoint = serviceUrl + testCase.Endpoint,
            Success = true,
            Messages = [],
        };

        try
        {
            if (testCase.UseIdFrom != null)
            {

                test.Ids = await FetchIds(serviceUrl + testCase.Endpoint, schemaVersion);
                test.Id = test.Ids.First();
            }

            var apiResponse = await _requestService.GetApiResponse(serviceUrl + testCase.Endpoint + test.Id);
            if (apiResponse.IsFailed)
            {
                test.Success = false;
                test.Messages = [new Issue
                {
                    Name = "API response issue",
                    Message = apiResponse.Errors.First().Message
                }];

                return test;
            }

            var schema = await ReadSchemaFromFile(testCase.Schema);
            var issues = ValidateResponseSchema(apiResponse.Value, schema);

            if (testCase.Pagination)
            {
                var paginationValidationResponse =
                    await _paginationTestingService.ValidatePagination(serviceUrl, testCase.Endpoint, schemaVersion);
                issues.Value.AddRange(paginationValidationResponse.Value);
            }

            test.Success = issues.IsSuccess && issues.Value.Count == 0;
            test.Messages.AddRange(issues.Value);
            return test;
        }
        
        catch (JsonSerializationException ex)
        {
            _logger.LogError(ex, "Error occurred while serializing JSON response for {endpoint}", testCase.Endpoint);

            test.Success = false;
            test.Messages = [
                new Issue
                {
                    Name = testCase.Name,
                    Description =  $"Error occurred while serializing JSON response for {testCase.Endpoint}",
                    Message = ex.Message
                }];

            return test;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error encountered when testing {endpoint} ", testCase.Endpoint);

            test.Success = false;
            test.Messages = [
                new Issue
                {
                    Name = testCase.Name,
                    Description =  $"An error encountered when testing {testCase.Endpoint}",
                    Message = e.Message
                }];

            return test;
        }
    }

    public Result<List<Issue>> ValidateResponseSchema(JsonNode response, JSchema schema)
    {
        var responseJToken = JToken.Parse(response.ToString());

        var isValid = responseJToken.IsValid(schema, out IList<ValidationError> errors);

        if (isValid)
            return new List<Issue>();

        return errors.Select(error => new Issue
        {
            Description = "Schema validation issue",
            Name = error.ErrorType.ToString(),
            Message = error.Message,
            ErrorAt = $"{error.Path}, line {error.LineNumber}, position {error.LinePosition}",
            ErrorIn = error.SchemaId!.ToString()
        }).ToList();
    }

    public async Task<List<string>> FetchIds(string url, string schemaVersion)
    {
        var random = new Random();
        var ids = new List<string>();
        const int idCountLimit = 3;
        var page = 1;

        while (ids.Count < idCountLimit)
        {
            var requestUrl = $"{url}?page={page}";
            var apiResponse = await _requestService.GetApiResponse(requestUrl);

            if (apiResponse.IsFailed)
                break;

            IPage? pagedData = schemaVersion == HSDSUKVersions.V3
                ? JsonConvert.DeserializeObject<PageV3>(apiResponse.ValueOrDefault.ToJsonString())
                : JsonConvert.DeserializeObject<PageV1>(apiResponse.ValueOrDefault.ToJsonString());

            if (pagedData == null)
                break;

            var pageSize = pagedData.Size;
            var contentObject = pagedData.Contents[random.Next(0, pageSize - 1)];

            ids.Add(contentObject!.Id!);

            var isLastPage = pagedData.LastPage;

            if (page == 1 && isLastPage && pageSize < idCountLimit)
                break;

            var totalPages = pagedData.TotalPages;
            page = random.Next(1, totalPages);
        }

        return ids;
    }

    private async Task<JSchema> ReadSchemaFromFile(string schema)
    {
        var schemaPath = "Schemas/" + schema;

        // Open the text file using a stream reader.
        using StreamReader reader = new(schemaPath);
        // Read the stream as a string.
        var fileContent = await reader.ReadToEndAsync();
        return JSchema.Parse(fileContent);
    }
}