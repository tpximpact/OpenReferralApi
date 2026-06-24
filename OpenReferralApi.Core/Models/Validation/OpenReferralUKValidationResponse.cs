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
    public List<object> TestSuites { get; set; } = [];

    /// <summary>
    /// Specification-level validation findings (for example OpenAPI schema/profile comparison errors)
    /// </summary>
    [JsonPropertyName("specificationValidation")]
    public object? SpecificationValidation { get; set; }

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
