using System.Text.Json.Nodes;
using FluentResults;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
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

        var isUrlValid = Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uriResult) 
                         && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        
        if (!isUrlValid)
            return Result.Fail("Invalid URL provided");

        var (testSchema, schemaReason) = await _testProfileService.SelectTestSchema(serviceUrl, profile);
        var testProfile = await _testProfileService.ReadTestProfileFromFile(testSchema);
        
        var validationResponse = new ValidationResponse
        {
            Service = new ServiceResponse
            {
                Url = serviceUrl,
                IsValid = true,
                Profile = testProfile.Value.Profile,
                ProfileReason = schemaReason
            },
            TestSuites = new List<TestGroup>(),
            Metadata = new List<MetaData>() // TODO Add meta data if available
        };

        foreach (var testGroup in testProfile.Value.TestGroups)
        {
            var testGroupResult = new TestGroup
            {
                Name = testGroup.Name,
                Description = testGroup.Description,
                Required = testGroup.Required,
                MessageLevel = testGroup.MessageLevel,
                Success = true,
                Tests = new List<Test>()
            };
            
            foreach (var testCase in testGroup.Tests)
            {
                try
                {
                    var testResult = await ValidateTestCase(testCase, serviceUrl);
                    if (testResult.IsFailed)
                        return Result.Fail(testResult.Errors);

                    testGroupResult.Tests.Add(testResult.Value);

                    if (!testResult.Value.Success)
                        testGroupResult.Success = false;
                }
                catch (Exception e)
                {
                    _logger.LogError("Error encountered when testing endpoint " + testCase.Endpoint);
                    _logger.LogError(e.Message);
                    testGroupResult.Tests.Add(new Test()
                    {   
                        Name = testCase.Name,
                        Description = testCase.Description,
                        Endpoint = testCase.Endpoint,
                        Success = false,
                        Messages = new List<Issue>()
                        {
                            new Issue()
                            {
                                Name = "Critical failure",
                                Description = "A critical failure occured whilst testing the endpoint",
                                Message = "A critical failure occured whilst testing the endpoint"
                            }
                        }
                    });
                }
            }
            
            validationResponse.TestSuites.Add(testGroupResult);
            
            if (testGroup.Required && !testGroupResult.Success)
                validationResponse.Service.IsValid = false;
        }

        return validationResponse;
    }

    private async Task<Result<Test>> ValidateTestCase(TestCase testCase, string serviceUrl)
    {
        var test = new Test
        {
            Name = testCase.Name,
            Description = testCase.Description,
            Endpoint = serviceUrl + testCase.Endpoint,
            Success = true,
            Messages = new List<Issue>()
        };

        if (testCase.UseIdFrom != null)
        {
            try
            {
                test.Ids = await FetchIds(serviceUrl + testCase.Endpoint);
                test.Id = test.Ids.First();
            }
            catch (Exception e)
            {
                _logger.LogError("Error encountered when validating the test case");
                _logger.LogError(e.Message);
                test.Success = false;
                test.Messages.Add(new Issue 
                    { Name = "API issue", Message = "Could not get a valid `id` for the request" }
                );
                return test;
            }
        }
        
        var apiResponse = await _requestService.GetApiResponse(serviceUrl + testCase.Endpoint + test.Id);
        if (apiResponse.IsFailed)
        {
            test.Success = false;
            test.Messages.Add(new Issue 
                { Name = "API response issue", Message = apiResponse.Errors.First().Message}
            );
            return test;
        }

        var schema = await ReadSchemaFromFile(testCase.Schema);
        var issues = ValidateResponseSchema(apiResponse.Value, schema);

        if (testCase.Pagination)
        {
            var paginationValidationResponse =
                await _paginationTestingService.ValidatePagination(serviceUrl, testCase.Endpoint);
            issues.Value.AddRange(paginationValidationResponse.Value);
        }

        test.Success = issues.IsSuccess && issues.Value.Count == 0;
        test.Messages.AddRange(issues.Value);
        return test;

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

    public async Task<List<string>> FetchIds(string url)
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
            
            var jsonResponse = apiResponse.Value;
            
            var pageSize = jsonResponse["size"]!.GetValue<int>();
            ids.Add(jsonResponse["contents"]![random.Next(0, pageSize - 1)]!["id"]!.GetValue<string>());
            
            var isLastPage = jsonResponse["last_page"]!.GetValue<bool>();

            if (page == 1 && isLastPage && pageSize < idCountLimit)
                break;
            
            var totalPages = jsonResponse["total_pages"]!.GetValue<int>();
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