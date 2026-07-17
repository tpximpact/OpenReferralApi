using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Endpoints;

public enum EndpointTestStatus
{
    /// <summary>
    /// Endpoint was discovered but not tested yet.
    /// </summary>
    NotTested,

    /// <summary>
    /// Endpoint was intentionally skipped by configuration or runtime conditions.
    /// </summary>
    Skipped,

    /// <summary>
    /// Endpoint test succeeded and response passed validation.
    /// </summary>
    PassedValidation,

    /// <summary>
    /// Endpoint test succeeded with non-blocking validation warnings.
    /// </summary>
    PassedWithWarnings,

    /// <summary>
    /// Endpoint test completed but response failed validation.
    /// </summary>
    FailedValidation,

    /// <summary>
    /// Endpoint test failed due to an unexpected execution error.
    /// </summary>
    Error
}

/// <summary>
/// Represents the results of testing a single API endpoint, including HTTP tests, validation, and performance metrics
/// </summary>
public class EndpointTestResult
{
    private List<ValidationError>? _validationErrors;

    /// <summary>
    /// The URL path of the endpoint being tested (e.g., "/users/{id}", "/orders")
    /// Used to identify which endpoint this result corresponds to
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method used for testing this endpoint (e.g., "GET", "POST", "PUT")
    /// Distinguishes between different operations on the same path
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// The unique operation identifier from the OpenAPI specification, if provided
    /// Useful for referencing specific operations and generating code/documentation
    /// </summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; set; }

    /// <summary>
    /// The name of the endpoint as defined in the OpenAPI specification
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; internal set; }

    /// <summary>
    /// Brief description of what this endpoint does, extracted from the OpenAPI specification
    /// Provides context for understanding the endpoint's purpose
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Indicates whether this endpoint is marked as optional in the OpenAPI specification
    /// </summary>
    [JsonPropertyName("isOptional")]
    public bool IsOptional { get; internal set; }

    /// <summary>
    /// Indicates whether actual HTTP testing was performed on this endpoint
    /// False if testing was skipped due to configuration, errors, or missing requirements
    /// </summary>
    [JsonPropertyName("isTested")]
    public bool IsTested { get; set; }

    /// <summary>
    /// Overall status of the endpoint test.
    /// Provides a quick summary of the testing outcome for dashboard/reporting purposes
    /// </summary>
    [JsonPropertyName("status")]
    public EndpointTestStatus Status { get; set; } = EndpointTestStatus.NotTested;

    /// <summary>
    /// Collection of HTTP test results for this endpoint, including request/response details
    /// May contain multiple results if the endpoint was tested with different parameters or conditions
    /// </summary>
    [JsonPropertyName("testResults")]
    public List<HttpTestResult> TestResults { get; set; } = [];

    /// <summary>
    /// Flattened validation errors aggregated across all test results.
    /// Provides direct access to endpoint-level issues without traversing nested test result objects.
    /// </summary>
    [JsonPropertyName("validationErrors")]
    public List<ValidationError> ValidationErrors
    {
        get => _validationErrors ??= AggregateValidationErrors(TestResults);
        set => _validationErrors = value;
    }

    /// <summary>
    /// First failing test result for quick diagnostics, or first available result if none failed.
    /// </summary>
    [JsonIgnore]
    [JsonPropertyName("primaryTestResult")]
    internal HttpTestResult? PrimaryTestResult =>
        TestResults.FirstOrDefault(tr => tr.ValidationResult != null && !tr.ValidationResult.IsValid)
        ?? TestResults.FirstOrDefault();

    /// <summary>
    /// Rebuilds the flattened endpoint-level error cache from the current test results.
    /// </summary>
    public void RefreshFlattenedFields()
    {
        _validationErrors = AggregateValidationErrors(TestResults);
    }

    private static List<ValidationError> AggregateValidationErrors(IEnumerable<HttpTestResult> testResults)
    {
        return [.. testResults
            .Where(tr => tr.ValidationResult != null)
            .SelectMany(tr => tr.ValidationResult!.Errors)
            .GroupBy(e => $"{e.Path}|{e.ErrorCode}|{e.Message}|{e.Severity}", StringComparer.Ordinal)
            .Select(g => g.First())];
    }

}
