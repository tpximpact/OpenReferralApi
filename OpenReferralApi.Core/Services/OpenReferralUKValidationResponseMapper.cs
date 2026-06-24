namespace OpenReferralApi.Core.Services;

/// <summary>
/// Maps OpenAPI validation results to the standard ValidationResponse format
/// </summary>
public interface IOpenReferralUKValidationResponseMapper
{
    OpenReferralUKValidationResponse MapToOpenReferralUKValidationResponse(OpenApiValidationResult openApiResult);
}

public class OpenReferralUKValidationResponseMapper : IOpenReferralUKValidationResponseMapper
{
    public OpenReferralUKValidationResponse MapToOpenReferralUKValidationResponse(OpenApiValidationResult openApiResult)
    {
        var testSuites = new List<object>();

        // Map endpoint tests to test groups - separate required and optional endpoints
        if (openApiResult.EndpointTests != null && openApiResult.EndpointTests.Count > 0)
        {
            var requiredEndpoints = new List<EndpointTestResult>();
            var optionalEndpoints = new List<EndpointTestResult>();

            foreach (var e in openApiResult.EndpointTests)
            {
                if (e.IsOptional)
                    optionalEndpoints.Add(e);
                else
                    requiredEndpoints.Add(e);
            }

            if (requiredEndpoints.Count > 0)
            {
                testSuites.Add(MapEndpointTests(requiredEndpoints, openApiResult?.Metadata?.BaseUrl ?? "",
                    "Level 1 Compliance - Basic checks",
                    "Will validate the required basic endpoints. Validation will fail if it does not pass all these checks.",
                    true));
            }

            if (optionalEndpoints.Count > 0)
            {
                testSuites.Add(MapEndpointTests(optionalEndpoints, openApiResult?.Metadata?.BaseUrl ?? "",
                    "Level 2 Compliance - Extended checks",
                    "Will validate all other endpoints. Validation will not fail if it does not pass all these checks.",
                    false));
            }
        }

        // Determine overall validity from the fully computed result on the validation result object,
        // which accounts for endpoint failures (FailedValidation and Error), specification validation errors,
        // and optional-endpoint treatment rules.
        bool isValid = openApiResult?.IsValid ?? false;

        object? specificationValidation = null;

        if (openApiResult?.SpecificationValidation != null)
        {
            var specErrors = new List<object>(openApiResult.SpecificationValidation.Errors.Count);
            foreach (var error in openApiResult.SpecificationValidation.Errors)
            {
                specErrors.Add(new
                {
                    name = error.ErrorCode,
                    description = error.Severity,
                    message = error.Message,
                    errorIn = BuildErrorIn(error),
                    errorAt = BuildErrorAt(error)
                });
            }

            specificationValidation = new
            {
                isValid = openApiResult.SpecificationValidation.IsValid,
                version = openApiResult.SpecificationValidation.Version,
                errors = specErrors
            };
        }

        return new OpenReferralUKValidationResponse
        {
            Service = new ServiceInfo
            {
                Url = openApiResult?.Metadata?.BaseUrl ?? "",
                IsValid = isValid,
                Profile = openApiResult?.Metadata?.Profile
                    ?? openApiResult?.SpecificationValidation?.Version ?? "Unknown",
                ProfileReason = openApiResult?.Metadata?.ProfileReason ?? "Unknown"
            },
            TestSuites = testSuites,
            SpecificationValidation = specificationValidation,
            Notifications = openApiResult?.Notifications?.ToList() ?? []
        };
    }

    private static string BuildErrorAt(ValidationError error)
    {
        if (error.LineNumber.HasValue && error.ColumnNumber.HasValue)
        {
            return $"line {error.LineNumber.Value}, column {error.ColumnNumber.Value}";
        }

        if (error.LineNumber.HasValue)
        {
            return $"line {error.LineNumber.Value}";
        }

        if (error.ColumnNumber.HasValue)
        {
            return $"column {error.ColumnNumber.Value}";
        }

        return string.Empty;
    }

    private static string BuildErrorIn(ValidationError error)
    {
        if (IsJsonStructureViolation(error.ErrorCode) && !string.IsNullOrWhiteSpace(error.SourceIdentifier))
        {
            return string.Concat("source=", error.SourceIdentifier, " | path=", error.Path);
        }

        return error.Path;
    }

    private static bool IsJsonStructureViolation(string? errorCode)
    {
        return string.Equals(errorCode, "JSON_STRUCTURE_VIOLATION", StringComparison.Ordinal)
            || string.Equals(errorCode, "CACHED_SCHEMA_STRUCTURE_VIOLATION", StringComparison.Ordinal)
            || string.Equals(errorCode, "SCHEMA_STRUCTURE_VIOLATION", StringComparison.Ordinal);
    }

    private static object MapEndpointTests(List<EndpointTestResult> endpointTests, string baseUrl,
        string name, string description, bool required)
    {
        var tests = new List<object>(endpointTests.Count);
        var seenErrorPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var endpoint in endpointTests)
        {
            seenErrorPaths.Clear(); // Reuse the same HashSet for every endpoint test
            var testToUse = endpoint.PrimaryTestResult;

            tests.Add(new
            {
                name = endpoint.Name ?? string.Concat(endpoint.Method, " ", endpoint.Path),
                endpoint = string.Concat(baseUrl, endpoint.Path),
                description = endpoint.Summary ?? endpoint.OperationId ?? "Endpoint test",
                id = testToUse?.TestedId,
                success = endpoint.PrimaryTestResult?.ValidationResult?.IsValid ?? endpoint.TestResults.Any(tr => tr.ValidationResult != null && tr.ValidationResult.IsValid),
                messages = MapEndpointMessages(endpoint, testToUse, seenErrorPaths)
            });
        }

        return new
        {
            name,
            description,
            messageLevel = required ? "error" : "warning",
            required,
            success = endpointTests.All(e => e.Status == EndpointTestStatus.PassedValidation || e.Status == EndpointTestStatus.PassedWithWarnings),
            tests
        };
    }

    private static List<object> MapEndpointMessages(EndpointTestResult endpoint, HttpTestResult? specificTest, HashSet<string> seenErrorPaths)
    {
        var messages = new List<object>();

        var endpointErrors = endpoint.ValidationErrors;

        if (specificTest == null && endpointErrors.Count > 0)
        {
            foreach (var validationError in endpointErrors)
            {
                if (seenErrorPaths.Add(validationError.Path))
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

            return messages;
        }

        if (specificTest != null)
        {
            if (specificTest.ValidationResult != null && !specificTest.ValidationResult.IsValid)
            {
                ExtractMessagesFromTestResult(specificTest, seenErrorPaths, messages);
            }
        }
        else
        {
            foreach (var testResult in endpoint.TestResults)
            {
                if (testResult.ValidationResult != null && !testResult.ValidationResult.IsValid)
                {
                    ExtractMessagesFromTestResult(testResult, seenErrorPaths, messages);
                }
            }
        }

        // If endpoint succeeded but had performance issues, add info messages
        if (endpoint.Status == EndpointTestStatus.PassedValidation && endpoint.TestResults.Count > 0)
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

    private static void ExtractMessagesFromTestResult(HttpTestResult testResult, HashSet<string> seenErrorPaths, List<object> messages)
    {
        if (testResult.ValidationResult?.Errors == null) return;

        foreach (var validationError in testResult.ValidationResult.Errors)
        {
            if (seenErrorPaths.Add(validationError.Path))
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
    }
}
