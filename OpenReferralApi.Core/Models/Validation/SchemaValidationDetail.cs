using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Validation;

/// <summary>
/// Detailed validation results for request or response schema compliance
/// </summary>
public class SchemaValidationDetail
{
    /// <summary>
    /// Where this validation was applied ("request" or "response")
    /// Distinguishes between input validation and output validation results
    /// </summary>
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Overall validation status ("Valid", "Invalid", "Skipped")
    /// Provides a quick summary of the validation outcome
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Specific validation errors found during schema checking
    /// Details about data structure violations or type mismatches
    /// Use the Severity property on ValidationError to distinguish between errors ("Error") and warnings ("Warning")
    /// </summary>
    [JsonPropertyName("errors")]
    public List<ValidationError> Errors { get; set; } = [];
}
