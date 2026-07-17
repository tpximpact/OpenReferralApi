using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Validation;

/// <summary>
/// Request model for initiating OpenAPI specification validation and testing
/// Contains all necessary information to validate specs and optionally test live endpoints
/// </summary>
public class OpenApiValidationRequest
{

    /// <summary>
    /// URL to fetch the OpenAPI specification from (JSON or YAML)
    /// The service will download and parse the specification from this URL
    /// Supports HTTP/HTTPS URLs and handles $ref resolution for external references
    /// </summary>
    [JsonPropertyName("ownSchemaUrl")]
    public string? OwnSchemaUrl { get; set; }

    /// <summary>
    /// Base URL of the live API server for endpoint testing
    /// Required if endpoint testing is enabled in options
    /// Should include protocol (http/https) and may include port (e.g., "https://api.example.com:8080")
    /// </summary>
    /// <example>https://api.example.org</example>
    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Authentication credentials and configuration for accessing the API server during endpoint testing
    /// Supports API keys, bearer tokens, basic auth, and custom headers
    /// Required if endpoint testing is enabled and the API requires authentication for access
    /// </summary>
    /// <example>{"method":"BearerToken","token":"sample-token"}</example>
    [JsonPropertyName("dataSourceAuth")]
    public DataSourceAuthentication? DataSourceAuth { get; set; }

    /// <summary>
    /// Configuration options controlling validation behavior and endpoint testing
    /// Determines what types of validation and testing to perform
    /// If null, default options will be used (specification validation only)
    /// </summary>
    /// <example>{"includeResponseBody":false,"includeTestResults":true}</example>
    [JsonPropertyName("options")]
    public OpenApiValidationOptions? Options { get; set; }

    /// <summary>
    /// Explicit profile version to override or bypass automatic profile discovery (e.g. "HSDS-UK-1.0")
    /// </summary>
    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

}
