namespace OpenReferralApi.Swagger;

internal static class FeedValidationOperationExamples
{
    private const string FeedListResponseExample = """
[
    {
        "id": "67f6fa9f5cb2fc547f5e2b10",
        "name": "Example Service Feed",
        "url": "https://api.example.org",
        "isUp": true,
        "isValid": true,
        "lastChecked": "2026-03-28T20:00:00Z"
    }
]
""";

    private const string FeedValidateAllResponseExample = """
{
    "totalFeeds": 1,
    "upFeeds": 1,
    "validFeeds": 1,
    "downFeeds": 0,
    "invalidFeeds": 0,
    "averageResponseTimeMs": 123.4,
    "results": [
        {
            "feedId": "67f6fa9f5cb2fc547f5e2b10",
            "feedName": "Example Service Feed",
            "feedUrl": "https://api.example.org",
            "isUp": true,
            "isValid": true,
            "responseTimeMs": 123.4,
            "validationErrorCount": 0
        }
    ]
}
""";

    private const string FeedValidateSingleResponseExample = """
{
    "feedId": "67f6fa9f5cb2fc547f5e2b10",
    "feedName": "Example Service Feed",
    "feedUrl": "https://api.example.org",
    "isUp": true,
    "isValid": true,
    "responseTimeMs": 123.4,
    "validationErrorCount": 0
}
""";

    private const string FeedNotFoundResponseExample = """
{
    "error": "Feed not found",
    "feedId": "67f6fa9f5cb2fc547f5e2b10"
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
        return new[]
        {
            new OperationExampleRegistration(
                "/api/feedvalidation/feeds",
                HttpMethod.Get,
                RequestExample: null,
                new Dictionary<string, string>
                {
                    ["200"] = FeedListResponseExample,
                    ["429"] = ProblemDetailsRateLimitExample,
                    ["500"] = ProblemDetailsServerErrorExample
                }),
            new OperationExampleRegistration(
                "/api/feedvalidation/validate-all",
                HttpMethod.Post,
                RequestExample: null,
                new Dictionary<string, string>
                {
                    ["200"] = FeedValidateAllResponseExample,
                    ["429"] = ProblemDetailsRateLimitExample,
                    ["500"] = ProblemDetailsServerErrorExample
                }),
            new OperationExampleRegistration(
                "/api/feedvalidation/validate/{feedId}",
                HttpMethod.Post,
                RequestExample: null,
                new Dictionary<string, string>
                {
                    ["200"] = FeedValidateSingleResponseExample,
                    ["404"] = FeedNotFoundResponseExample,
                    ["429"] = ProblemDetailsRateLimitExample,
                    ["500"] = ProblemDetailsServerErrorExample
                })
        };
    }
}
