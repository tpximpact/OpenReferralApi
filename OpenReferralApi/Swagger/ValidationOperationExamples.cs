namespace OpenReferralApi.Swagger;

internal static class ValidationOperationExamples
{
    private const string ValidationRequestExample = """
{
    "openApiSchema": {
        "url": "https://example.org/openapi.json"
    },
    "baseUrl": "https://api.example.org",
    "options": {
        "includeResponseBody": false,
        "includeTestResults": true
    }
}
""";

    private const string OpenReferralValidationResponseExample = """
{
    "isValid": true,
    "summary": {
        "totalEndpoints": 42,
        "successfulTests": 42,
        "failedTests": 0,
        "skippedTests": 0
    },
    "notifications": [],
    "metadata": {
        "profile": "HSDS-UK-3.0"
    }
}
""";

    private const string OpenReferralUkValidationResponseExample = """
{
    "service": {
        "url": "https://api.example.org",
        "isValid": true,
        "profile": "HSDS-UK-3.0",
        "profileReason": "Matched configured schema URL"
    },
    "testSuites": [],
    "specificationValidation": null,
    "notifications": []
}
""";

    private const string ValidationProblemResponseExample = """
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "errors": {
        "request": [
            "OpenAPI schema URL must be provided or discoverable from baseUrl"
        ]
    }
}
""";

    private const string ProblemDetailsRateLimitExample = """
{
    "type": "https://tools.ietf.org/html/rfc6585#section-4",
    "title": "Too Many Requests",
    "status": 429
}
""";

    private const string ProblemDetailsServerErrorExample = """
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
    "title": "An error occurred while processing your request.",
    "status": 500
}
""";

    public static IReadOnlyList<OperationExampleRegistration> GetRegistrations()
    {
        var commonResponses = new Dictionary<string, string>
        {
            ["400"] = ValidationProblemResponseExample,
            ["429"] = ProblemDetailsRateLimitExample,
            ["500"] = ProblemDetailsServerErrorExample
        };

        return new[]
        {
            new OperationExampleRegistration(
                "/openreferral/validate",
                HttpMethod.Post,
                ValidationRequestExample,
                new Dictionary<string, string>(commonResponses)
                {
                    ["200"] = OpenReferralValidationResponseExample
                }),
            new OperationExampleRegistration(
                "/openreferraluk/validate",
                HttpMethod.Post,
                ValidationRequestExample,
                new Dictionary<string, string>(commonResponses)
                {
                    ["200"] = OpenReferralUkValidationResponseExample
                }),
            new OperationExampleRegistration(
                "/api/openapi/validate",
                HttpMethod.Post,
                ValidationRequestExample,
                new Dictionary<string, string>(commonResponses)
                {
                    ["200"] = OpenReferralUkValidationResponseExample
                })
        };
    }
}
