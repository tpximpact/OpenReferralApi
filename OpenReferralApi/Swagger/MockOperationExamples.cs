namespace OpenReferralApi.Swagger;

internal static class MockOperationExamples
{
    private const string MockApiDetailsResponseExample = """
{
    "api_version": "3.0",
    "data": {
        "id": "example-api",
        "name": "Example Open Referral API",
        "description": "Mock metadata payload"
    }
}
""";

    private const string MockServiceListResponseExample = """
{
    "data": [
        {
            "id": "service-001",
            "name": "Food Bank Support",
            "description": "Support with emergency food parcels"
        }
    ]
}
""";

    private const string MockServiceDetailResponseExample = """
{
    "data": {
        "id": "service-001",
        "name": "Food Bank Support",
        "description": "Support with emergency food parcels",
        "status": "active"
    }
}
""";

    private const string MockOrganizationListResponseExample = """
{
    "data": [
        {
            "id": "org-001",
            "name": "Example Community Trust"
        }
    ]
}
""";

    private const string MockOrganizationDetailResponseExample = """
{
    "data": {
        "id": "org-001",
        "name": "Example Community Trust",
        "description": "Community services provider"
    }
}
""";

    private const string MockV1ValidateResponseExample = """
{
    "isValid": true,
    "profile": "HSDS-UK-1.0",
    "errors": []
}
""";

    private const string MockV1DashboardResponseExample = """
{
    "summary": {
        "totalFeeds": 1,
        "validFeeds": 1,
        "invalidFeeds": 0
    }
}
""";

    private const string MockNotFoundResponseExample = """
{
    "error": "Mock file not found",
    "file": "Mocks/V3.0-UK-Default/service_list.json"
}
""";

    private const string MockServerErrorResponseExample = """
{
    "error": "Error reading mock file",
    "message": "The process cannot access the file because it is being used by another process."
}
""";

    public static IReadOnlyList<OperationExampleRegistration> GetRegistrations()
    {
        return new[]
        {
            CreateGet("/api/mock", MockApiDetailsResponseExample),
            CreateGet("/api/mock/services", MockServiceListResponseExample),
            CreateGet("/api/mock/services/{id}", MockServiceDetailResponseExample),
            CreateGet("/api/mock/organizations", MockOrganizationListResponseExample),
            CreateGet("/api/mock/organizations/{id}", MockOrganizationDetailResponseExample),
            CreateGet("/api/mock/v1/dashboard", MockV1DashboardResponseExample),
            new OperationExampleRegistration(
                "/api/mock/v1/validate",
                HttpMethod.Post,
                RequestExample: null,
                new Dictionary<string, string>
                {
                    ["200"] = MockV1ValidateResponseExample,
                    ["404"] = MockNotFoundResponseExample,
                    ["500"] = MockServerErrorResponseExample
                })
        };
    }

    private static OperationExampleRegistration CreateGet(string path, string successExample)
    {
        return new OperationExampleRegistration(
            path,
            HttpMethod.Get,
            RequestExample: null,
            new Dictionary<string, string>
            {
                ["200"] = successExample,
                ["404"] = MockNotFoundResponseExample,
                ["500"] = MockServerErrorResponseExample
            });
    }
}
