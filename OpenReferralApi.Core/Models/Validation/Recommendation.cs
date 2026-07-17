using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Validation;

/// <summary>
/// Represents an actionable recommendation for improving the OpenAPI specification based on analysis results
/// </summary>
public class Recommendation
{
    /// <summary>
    /// The type of recommendation ("Error", "Warning", "Improvement", "Security", "BestPractice")
    /// Categorizes the recommendation by its nature and urgency level
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The category this recommendation falls under ("Validation", "Documentation", "Security", "Performance", "Legal")
    /// Groups related recommendations for easier organization and prioritization
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The priority level of this recommendation ("High", "Medium", "Low")
    /// Helps teams prioritize which improvements to address first
    /// </summary>
    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// A clear, descriptive message explaining what needs to be addressed
    /// Provides the specific issue or improvement opportunity identified
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The specific path or location in the specification where this recommendation applies
    /// Helps developers quickly locate and fix the identified issue (e.g., "info.description", "paths./users.get")
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>
    /// Specific action steps that should be taken to address this recommendation
    /// Provides concrete guidance on how to implement the suggested improvement
    /// </summary>
    [JsonPropertyName("actionRequired")]
    public string? ActionRequired { get; set; }

    /// <summary>
    /// Description of the positive impact that implementing this recommendation will have
    /// Explains the benefits and why this change is worth making
    /// </summary>
    [JsonPropertyName("impact")]
    public string? Impact { get; set; }
}
