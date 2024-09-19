using System.Text.Json.Nodes;
using FluentResults;
using Json.Schema;
using Newtonsoft.Json;
using OpenReferralApi.Models;
using OpenReferralApi.Services.Interfaces;

namespace OpenReferralApi.Services;

public class ValidatorService : IValidatorService
{
    private const string Profile = "HSDS-3.0-UK-2";
    private readonly IRequestService _requestService; 

    public ValidatorService(IRequestService requestService)
    {
        _requestService = requestService;
    }

    public async Task<Result<ValidationResponse>> ValidateService(string serviceUrl)
    {
        var testProfile = await ReadTestProfileFromFile($"TestProfiles/{Profile}.json");
        
        var validationResponse = new ValidationResponse
        {
            ServiceUrl = serviceUrl,
            TestsProfile = Profile,
            Tests = new List<Test>(),
            AllTestsPassed = true,
            BasicTestsPassed = true
        };

        foreach (var testCase in testProfile.Value.TestCases)
        {
            var testResult = await ValidateTestCase(testCase, serviceUrl);
            if (testResult.IsFailed)
                return Result.Fail(testResult.Errors);

            validationResponse.Tests.Add(testResult.Value);
            if (!testResult.Value.Success)
                validationResponse.AllTestsPassed = false;

            if (testCase.TestLevel == 1 && !validationResponse.AllTestsPassed)
                validationResponse.BasicTestsPassed = false;
        }

        return validationResponse;
    }

    private async Task<Result<Test>> ValidateBaseEndpoint(string serviceUrl)
    {
        var response = await _requestService.GetApiDetails(serviceUrl);
        var schema = JsonSchema.FromFile("Schemas/ApiDetails.json");

        var issues = ValidateResponseSchema(response.Value, schema);

        return new Test
        {
            Name = "API meta info - API & schema validation",
            Description =
                "Does the base endpoint return meta information about the API, and does it adhere to the schema",
            Endpoint = "/",
            Success = issues.IsSuccess && issues.Value.Count == 0,
            Issues = issues.Value
        };
    }

    private async Task<Result<Test>> ValidateTestCase(TestCase testCase, string serviceUrl)
    {
        var apiResponse = await _requestService.GetApiResponse(serviceUrl, testCase.Endpoint);

        var schemaPath = "Schemas/" + testCase.Schema;
        var schema = JsonSchema.FromFile(schemaPath);

        var issues = ValidateResponseSchema(apiResponse.Value, schema);

        return new Test
        {
            Name = testCase.Name,
            Description = testCase.Description,
            Endpoint = testCase.Endpoint,
            Success = issues.IsSuccess && issues.Value.Count == 0,
            TestLevel = testCase.TestLevel,
            Issues = testCase.TestLevel == 1 ? issues.Value : new List<Issue>(),
            Warnings = testCase.TestLevel > 1 ? issues.Value : new List<Issue>()
        };
        
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
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
            return Result.Fail(e.Message);
        }
    } 
}