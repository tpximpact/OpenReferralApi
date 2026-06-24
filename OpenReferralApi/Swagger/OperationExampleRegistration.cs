namespace OpenReferralApi.Swagger;

internal sealed record OperationExampleRegistration(
    string Path,
    HttpMethod Method,
    string? RequestExample,
    IReadOnlyDictionary<string, string> ResponseExamples);
