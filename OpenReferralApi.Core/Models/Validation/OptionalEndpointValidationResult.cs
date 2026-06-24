using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Validation;

/// <summary>
/// Result of validating an optional endpoint response
/// </summary>
public class OptionalEndpointValidationResult
{
    [JsonPropertyName("isOptional")]
    public bool IsOptional { get; set; }

    [JsonPropertyName("validationStatus")]
    public OptionalEndpointStatus ValidationStatus { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("requiresSchemaValidation")]
    public bool RequiresSchemaValidation { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
