using System.Diagnostics;
using System.Text.Json.Nodes;
using FluentResults;
using Json.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenReferralApi.Models;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class ValidatorService : IValidatorService
{
    private const string V3Profile = "HSDS-UK-3.0";
    private const string V1Profile = "HSDS-UK-1.0";
    private readonly IRequestService _requestService;
    private readonly ILogger<ValidatorService> _logger;
    private Dictionary<string, string> _savedFields;

    public ValidatorService(IRequestService requestService, ILogger<ValidatorService> logger)
    {
        _requestService = requestService;
        _logger = logger;
        _savedFields = new Dictionary<string, string>();
    }

    public async Task<Result<ValidationResponse>> ValidateService(string serviceUrl, string? profile)
    {
        serviceUrl = serviceUrl.TrimEnd('/');

        var isUrlValid = Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uriResult) 
                         && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        
        if (!isUrlValid)
            return Result.Fail("Invalid URL provided");

        var testSchema = await SelectTestSchema(serviceUrl, profile);
        var testProfile = await ReadTestProfileFromFile($"TestProfiles/{testSchema}.json");
        
        var validationResponse = new ValidationResponse
        {
            Service = new ServiceDetails
            {
                Url = serviceUrl,
                IsValid = true,
                Profile = testProfile.Value.Profile
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
                var testResult = await ValidateTestCase(testCase, serviceUrl);
                if (testResult.IsFailed)
                    return Result.Fail(testResult.Errors);

                testGroupResult.Tests.Add(testResult.Value);

                if (!testResult.Value.Success)
                    testGroupResult.Success = false;
            }
            
            validationResponse.TestSuites.Add(testGroupResult);
            
            if (testGroup.Required && !testGroupResult.Success)
                validationResponse.Service.IsValid = false;
        }

        return validationResponse;
    }

    private async Task<string> SelectTestSchema(string serviceUrl, string? profileInput)
    {
        try
        {
            if (!string.IsNullOrEmpty(profileInput))
            {
                return profileInput switch
                {
                    V1Profile => V1Profile,
                    _ => V3Profile
                };
            }

            var apiResult = await _requestService.GetApiResponse(serviceUrl, "/");
            if (apiResult.IsFailed) 
                return V1Profile;
            
            return apiResult.Value["version"]!.ToString() switch
            {
                V1Profile => V1Profile,
                _ => V3Profile
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Error encountered when selecting the test schema");
            _logger.LogError(e.Message);
        }

        return V3Profile;
    }

    private async Task<Result<Test>> ValidateTestCase(TestCase testCase, string serviceUrl)
    {
        var test = new Test
        {
            Name = testCase.Name,
            Description = testCase.Description,
            Endpoint = testCase.Endpoint,
            Success = true,
            Messages = new List<Issue>()
        };

        var endpoint = testCase.Endpoint;
        if (testCase.UseIdFrom != null)
        {
            try
            {
                endpoint += _savedFields[testCase.UseIdFrom];
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
        
        var apiResponse = await _requestService.GetApiResponse(serviceUrl, endpoint);
        if (apiResponse.IsFailed)
        {
            test.Success = false;
            test.Messages.Add(new Issue 
                { Name = "API response issue", Message = apiResponse.Errors.First().Message }
            );
            return test;
        }

        var schemaPath = "Schemas/" + testCase.Schema;
        var schema = JsonSchema.FromFile(schemaPath);

        var issues = ValidateResponseSchema(apiResponse.Value, schema);

        if (testCase.SaveIds)
        {
            var fieldValue = apiResponse.Value[testCase.SaveIdField]![0]!["id"];
            if (fieldValue != null) 
                _savedFields.Add($"{testCase.Endpoint}-id", fieldValue.ToString());
        }
        
        if (testCase.Pagination)
        {
            var paginationValidationResponse = await ValidatePagination(testCase, serviceUrl, apiResponse.Value);
            issues.Value.AddRange(paginationValidationResponse.Value);
        }

        test.Success = issues.IsSuccess && issues.Value.Count == 0;
        test.Messages.AddRange(issues.Value);
        return test;

    }

    private async Task<Result<List<Issue>>> ValidatePagination(TestCase testCase, string serviceUrl, JsonNode apiResponse)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        };
        var firstPage = JsonConvert.DeserializeObject<Page>(apiResponse.ToJsonString(), serializerSettings);
        var perPage = 20;
        var totalPages = firstPage!.TotalPages < 3 ? firstPage!.TotalPages : 3;
        if (firstPage!.TotalItems < 60 && totalPages > 0)
        {
            perPage = (firstPage.TotalItems + (totalPages - 1)) / totalPages;
        }

        var issues = new List<Issue>();
        
        // Request several pages and check the pagination meta data 
        for (var page = 1; page <= totalPages; page++)
        {
            var response = await _requestService.GetApiResponse(serviceUrl, testCase.Endpoint, perPage, page);
            if (response.IsFailed)
            {
                issues.Add(new Issue()
                {
                    Name = "API response",
                    Description = $"An error occurred when making a request to the `{testCase.Endpoint}` endpoint",
                    Message = response.Errors.First().Message,
                    Parameters = $"{testCase.Endpoint}?per_page={perPage}&page={page}"
                });
                continue;
            }
            var currentPage = JsonConvert.DeserializeObject<Page>(response.Value.ToJsonString(), serializerSettings);
            // Is the total number of items correct
            if (currentPage!.TotalItems != firstPage.TotalItems)
            {
                issues.Add(new Issue()
                {
                    Name = "Total items",
                    Description = "Is the total number of items correct",
                    Message = $"The value of 'total_items' has changed from {firstPage.TotalItems} to {currentPage.TotalItems} whilst requesting page {page} of the data",
                    Parameters = $"{testCase.Endpoint}?per_page={perPage}&page={page}"
                });
            }
            // Is the number of items returned per page correct
            if (page < totalPages && currentPage.Size != perPage)
            {
                issues.Add(new Issue()
                {
                    Name = "Items per page",
                    Description = "Is the number of items returned per page correct",
                    Message = $"The value of 'size' is {currentPage.Size} when {perPage} item(s) were requested in the 'per_page' parameter",
                    Parameters = $"{testCase.Endpoint}?per_page={perPage}&page={page}"
                });
            }
            // Does the number of items returned match the 'size' value in the response
            if (currentPage.Size != currentPage.Contents.Count)
            {
                issues.Add(new Issue()
                {
                    Name = "Item count",
                    Description = "Does the number of items returned match the 'size' value in the response",
                    Message = $"The value of 'size' is {currentPage.Size} when {currentPage.Contents.Count} item(s) were returned in the response content",
                    Parameters = $"{testCase.Endpoint}?per_page={perPage}&page={page}"
                });
            }
            // Is the 'first_page' flag returned correctly
            if ((page == 1 && !currentPage.FirstPage) || (page != 1 && currentPage.FirstPage))
            {
                issues.Add(new Issue()
                {
                    Name = "First page flag",
                    Description = "Is the 'first_page' flag returned correctly",
                    Message = $"The value of 'first_page' is {currentPage.FirstPage} when the page number is {page}",
                    Parameters = $"{testCase.Endpoint}?per_page={perPage}&page={page}"
                });
            }
            // Is the 'last_page' flag returned correctly
            if ((page == firstPage.TotalPages && !currentPage.LastPage) || (page != firstPage.TotalPages && currentPage.LastPage))
            {
                issues.Add(new Issue()
                {
                    Name = "Last page flag",
                    Description = "Is the 'last_page' flag returned correctly",
                    Message = $"The value of 'last_page' is {currentPage.LastPage} when the page number is {page} of {firstPage.TotalPages}",
                    Parameters = $"{testCase.Endpoint}?per_page={perPage}&page={page}"
                });
            }
        }
        
        return Result.Ok(issues);
    }

    private Result<List<Issue>> ValidateResponseSchema(JsonNode response, JsonSchema schema)
    {
        var evalOptions = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true
        };
            
        var results = schema.Evaluate(response, evalOptions);

        var issues = new List<Issue>();
        
        if (results.IsValid || !results.HasDetails) return issues;

        // TODO translate evaluation results into our output result
        foreach (var detail in results.Details)
        {
            if (!detail.HasErrors) continue;
                
            foreach (var error in detail.Errors!)
            {
                var issue = new Issue()
                {
                    Name = error.Key,
                    // TODO create a lookup to return a better description based on the error key 
                    Description = "A schema validation issue has been found",
                    Message = error.Value
                };
                issues.Add(issue);
            }
        }
        
        return issues;
    }

    private async Task<Result<TestProfile>> ReadTestProfileFromFile(string filePath)
    {
        try
        {
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