using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Validation;

/// <summary>
/// Represents the mapped validation response returned by the OpenAPI validation service
/// </summary>
public class OpenReferralUKValidationResponse
{
    /// <summary>
    /// Service information and overall validation status
    /// </summary>
    [JsonPropertyName("service")]
    public ServiceInfo Service { get; set; } = new();

    /// <summary>
    /// Collection of test suites containing endpoint validation results
    /// </summary>
    [JsonPropertyName("testSuites")]
    public List<TestSuiteInfo> TestSuites { get; set; } = [];

    /// <summary>
    /// Specification-level validation findings (for example OpenAPI schema/profile comparison errors)
    /// </summary>
    [JsonPropertyName("specificationValidation")]
    public SpecificationValidationResult? SpecificationValidation { get; set; }

    /// <summary>
    /// User-facing notifications about processing issues such as specification fetch/resolve failures
    /// </summary>
    [JsonPropertyName("notifications")]
    public List<string> Notifications { get; set; } = [];
}

/// <summary>
/// Contains service metadata and overall validation status
/// </summary>
public class ServiceInfo
{
    /// <summary>
    /// The base URL of the service being validated
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Whether the service passed validation
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// The OpenAPI specification version (e.g., "3.0.0", "2.0")
    /// </summary>
    [JsonPropertyName("profile")]
    public string Profile { get; set; } = "Unknown";

    /// <summary>
    /// Reason or explanation for the profile version
    /// </summary>
    [JsonPropertyName("profileReason")]
    public string ProfileReason { get; set; } = "Unknown";
}

/// <summary>
/// Represents a validation message/error payload item.
/// </summary>
public readonly record struct ValidationMessage(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("errorIn")] string ErrorIn,
    [property: JsonPropertyName("errorAt")] string ErrorAt,
    [property: JsonPropertyName("recordId")] string? RecordId = null
);

/// <summary>
/// Represents the structure for specification-level validation findings.
/// </summary>
public readonly record struct SpecificationValidationResult(
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("errors")] IReadOnlyList<ValidationMessage> Errors
);

/// <summary>
/// Represents validation details for an individual endpoint test.
/// </summary>
public readonly record struct EndpointTestInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("endpoint")] string Endpoint,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("messages")] IReadOnlyList<ValidationMessage> Messages
);

/// <summary>
/// Represents a group of endpoint tests representing a compliance validation suite.
/// </summary>
public readonly record struct TestSuiteInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("messageLevel")] string MessageLevel,
    [property: JsonPropertyName("required")] bool Required,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("tests")] IReadOnlyList<EndpointTestInfo> Tests
);
