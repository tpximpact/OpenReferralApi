using OpenReferralApi.Core.Models;

namespace OpenReferralApi.Core.Services;

/// <summary>
/// Maps OpenAPI validation results to the standard ValidationResponse format
/// </summary>
public interface IOpenApiToValidationResponseMapper
{
    object MapToValidationResponse(OpenApiValidationResult openApiResult);
}

public class OpenApiToValidationResponseMapper : IOpenApiToValidationResponseMapper
{
    public object MapToValidationResponse(OpenApiValidationResult openApiResult)
    {
        var testSuites = new List<object>();

        // Map specification validation to a test group
        if (openApiResult.SpecificationValidation != null)
        {
            testSuites.Add(MapSpecificationValidation(openApiResult.SpecificationValidation));
        }

        // Map endpoint tests to test groups
        if (openApiResult.EndpointTests != null && openApiResult.EndpointTests.Any())
        {
            testSuites.Add(MapEndpointTests(openApiResult.EndpointTests));
        }

        // Determine overall validity
        bool isValid = openApiResult.IsValid && 
                       openApiResult.Summary.FailedTests == 0 && 
                       (openApiResult.SpecificationValidation?.IsValid ?? true);

        return new
        {
            service = new
            {
                url = openApiResult.Metadata?.BaseUrl ?? "",
                isValid = isValid,
                profile = $"OpenAPI {openApiResult.SpecificationValidation?.OpenApiVersion ?? "Unknown"}",
                profileReason = "Validated against OpenAPI specification"
            },
            testSuites = testSuites
        };
    }

    private object MapSpecificationValidation(OpenApiSpecificationValidation specValidation)
    {
        var tests = new List<object>();

        // Main specification test
        tests.Add(new
        {
            name = "OpenAPI Specification Structure",
            endpoint = "",
            description = "Validates the OpenAPI specification structure and compliance",
            success = specValidation.IsValid,
            messages = specValidation.Errors.Select(e => new
            {
                name = e.ErrorCode,
                description = e.Severity,
                message = e.Message,
                errorIn = e.Path,
                errorAt = ""
            }).ToList()
        });

        // Quality metrics test
        if (specValidation.QualityMetrics != null)
        {
            var qualityIssues = new List<object>();
            
            if (specValidation.QualityMetrics.DocumentationCoverage < 80)
            {
                qualityIssues.Add(new
                {
                    name = "Documentation Coverage",
                    description = "Warning",
                    message = $"Documentation coverage is {specValidation.QualityMetrics.DocumentationCoverage:F1}%. Target is 80% or higher.",
                    errorIn = "info",
                    errorAt = ""
                });
            }

            tests.Add(new
            {
                name = "Documentation Quality",
                endpoint = "",
                description = $"Quality Score: {specValidation.QualityMetrics.QualityScore:F1}/100",
                success = qualityIssues.Count == 0,
                messages = qualityIssues
            });
        }

        return new
        {
            name = "OpenAPI Specification Validation",
            description = "Validates the structure, quality, and compliance of the OpenAPI specification",
            messageLevel = "ERROR",
            required = true,
            success = specValidation.IsValid,
            tests = tests
        };
    }

    private object MapEndpointTests(List<EndpointTestResult> endpointTests)
    {
        var tests = endpointTests.Select(endpoint => new
        {
            name = $"{endpoint.Method} {endpoint.Path}",
            endpoint = endpoint.Path,
            description = endpoint.Summary ?? endpoint.OperationId ?? "Endpoint test",
            success = endpoint.Status == "Success" || endpoint.Status == "Warning",
            messages = MapEndpointMessages(endpoint)
        }).ToList();

        return new
        {
            name = "Endpoint Testing",
            description = "Tests all API endpoints for availability and schema compliance",
            messageLevel = "ERROR",
            required = true,
            success = endpointTests.All(e => e.Status == "Success" || e.Status == "Warning"),
            tests = tests
        };
    }

    private List<object> MapEndpointMessages(EndpointTestResult endpoint)
    {
        var messages = new List<object>();

        // Add validation errors
        foreach (var error in endpoint.ValidationErrors)
        {
            messages.Add(new
            {
                name = error.ErrorCode,
                description = error.Severity,
                message = error.Message,
                errorIn = error.Path,
                errorAt = ""
            });
        }

        // Add HTTP test failures
        foreach (var testResult in endpoint.TestResults.Where(tr => !tr.IsSuccess))
        {
            messages.Add(new
            {
                name = "HTTP Request Failed",
                description = "Error",
                message = testResult.ErrorMessage ?? $"Request returned status {testResult.ResponseStatusCode}",
                errorIn = testResult.RequestUrl,
                errorAt = ""
            });
        }

        // Add schema validation issues from test results
        foreach (var testResult in endpoint.TestResults.Where(tr => tr.ValidationResult != null && !tr.ValidationResult.IsValid))
        {
            foreach (var validationError in testResult.ValidationResult!.Errors)
            {
                messages.Add(new
                {
                    name = validationError.ErrorCode,
                    description = validationError.Severity,
                    message = validationError.Message,
                    errorIn = validationError.Path,
                    errorAt = ""
                });
            }
        }

        // If endpoint succeeded but had performance issues, add info messages
        if (endpoint.Status == "Success" && endpoint.TestResults.Any())
        {
            var avgResponseTime = endpoint.TestResults
                .Where(tr => tr.ResponseTime > TimeSpan.Zero)
                .Average(tr => tr.ResponseTime.TotalMilliseconds);

            if (avgResponseTime > 5000) // Slow response warning
            {
                messages.Add(new
                {
                    name = "Performance",
                    description = "Warning",
                    message = $"Average response time is {avgResponseTime:F0}ms, which may be slow",
                    errorIn = endpoint.Path,
                    errorAt = ""
                });
            }
        }

        return messages;
    }
}
