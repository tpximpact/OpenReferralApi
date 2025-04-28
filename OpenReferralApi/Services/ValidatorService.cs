using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;
using OpenReferralApi.Models;
using OpenReferralApi.Models.Responses;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class ValidatorService : IValidatorService
{
    private readonly ILogger<ValidatorService> _logger;
    private readonly IRequestService _requestService;
    private readonly ITestProfileService _testProfileService;
    private Dictionary<string, string> _savedFields;

    public ValidatorService(ILogger<ValidatorService> logger, IRequestService requestService, ITestProfileService testProfileService)
    {
        _logger = logger;
        _requestService = requestService;
        _testProfileService = testProfileService;
        _savedFields = new Dictionary<string, string>();
    }

    public async Task<Result<ValidationResponse>> ValidateService(string serviceUrl, string? profile)
    {
        _savedFields.Clear();
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
                test.Id = _savedFields[testCase.UseIdFrom];
                await FetchIds(serviceUrl + testCase.Endpoint);
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

        var schemaPath = "Schemas/" + testCase.Schema;
        
        // Open the text file using a stream reader.
        using StreamReader reader = new(schemaPath);
        // Read the stream as a string.
        var fileContent = await reader.ReadToEndAsync();
        var jSchema = JSchema.Parse(fileContent);
        var issues = ValidateResponseSchema(apiResponse.Value, jSchema);

        if (testCase.SaveIds)
        {
            try
            {
                var fieldValue = apiResponse.Value[testCase.SaveIdField]?[0]?["id"];
                if (fieldValue != null) 
                    _savedFields.Add($"{testCase.Endpoint}-id", fieldValue.ToString());
            }
            catch (Exception e)
            {
                _logger.LogError("Error encountered when retrieving id required endpoint");
                _logger.LogError(e.Message);
            }
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

    public async Task<Result<List<Issue>>> ValidatePagination(TestCase testCase, string serviceUrl, JsonNode apiResponse)
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
            var parameters = new Dictionary<string, string>
            {
                { "perPage", perPage.ToString() }, 
                { "page", page.ToString() }
            };
            var endpoint = serviceUrl + testCase.Endpoint;
            endpoint = QueryHelpers.AddQueryString(endpoint, parameters!);
            
            var response = await _requestService.GetApiResponse(endpoint);
            if (response.IsFailed)
            {
                issues.Add(new Issue()
                {
                    Name = "API response",
                    Description = $"An error occurred when making a request to the `{testCase.Endpoint}` endpoint",
                    Message = response.Errors.First().Message,
                    Parameters = parameters,
                    Endpoint = endpoint
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
                    Parameters = parameters,
                    Endpoint = endpoint
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
                    Parameters = parameters,
                    Endpoint = endpoint
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
                    Parameters = parameters,
                    Endpoint = endpoint
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
                    Parameters = parameters,
                    Endpoint = endpoint
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
                    Parameters = parameters,
                    Endpoint = endpoint
                });
            }
        }
        
        return Result.Ok(issues);
    }

    public Result<List<Issue>> ValidateResponseSchema(JsonNode response, JSchema schema)
    {
        IList<ValidationError> errors;
        var rString = response.ToString();
        var jstring = JToken.Parse(rString);

        var isValid = jstring.IsValid(schema, out errors);

        if (isValid)
            return new List<Issue>();

        var issues = errors.Select(error => new Issue() 
        {
            Description = "Schema validation issue", 
            Name = error.ErrorType.ToString(), 
            Message = error.Message,
            ErrorAt = $"{error.Path}, line {error.LineNumber}, position {error.LinePosition}",
            ErrorIn = error.SchemaId!.ToString()
        }).ToList();
        
        return issues;
    }

    public async Task<Result<List<string>>> FetchIds(string url)
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
                return Result.Fail(apiResponse.Errors);
            
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
}