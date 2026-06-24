using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Validation;

/// <summary>
/// Comprehensive quality metrics measuring documentation completeness, best practices adherence, and developer experience
/// </summary>
public class QualityMetrics
{
    /// <summary>
    /// Percentage of endpoints that have meaningful descriptions (0-100)
    /// Higher percentages indicate better documentation quality and developer experience
    /// </summary>
    [JsonPropertyName("documentationCoverage")]
    public double DocumentationCoverage { get; set; }

    /// <summary>
    /// Number of endpoints that include description fields
    /// Descriptions help developers understand the purpose and behavior of each endpoint
    /// </summary>
    [JsonPropertyName("endpointsWithDescription")]
    public int EndpointsWithDescription { get; set; }

    /// <summary>
    /// Number of endpoints that include summary fields
    /// Summaries provide quick overviews of endpoint functionality
    /// </summary>
    [JsonPropertyName("endpointsWithSummary")]
    public int EndpointsWithSummary { get; set; }

    /// <summary>
    /// Number of endpoints that include request or response examples
    /// Examples are crucial for understanding expected data formats and testing
    /// </summary>
    [JsonPropertyName("endpointsWithExamples")]
    public int EndpointsWithExamples { get; set; }

    /// <summary>
    /// Number of parameters that include description fields
    /// Parameter descriptions help developers understand input requirements
    /// </summary>
    [JsonPropertyName("parametersWithDescription")]
    public int ParametersWithDescription { get; set; }

    /// <summary>
    /// Total number of parameters across all endpoints
    /// Used to calculate parameter documentation coverage percentages
    /// </summary>
    [JsonPropertyName("totalParameters")]
    public int TotalParameters { get; set; }

    /// <summary>
    /// Number of schema definitions that include description fields
    /// Schema descriptions help developers understand data model purposes and constraints
    /// </summary>
    [JsonPropertyName("schemasWithDescription")]
    public int SchemasWithDescription { get; set; }

    /// <summary>
    /// Total number of schema definitions in the specification
    /// Used to calculate schema documentation coverage percentages
    /// </summary>
    [JsonPropertyName("totalSchemas")]
    public int TotalSchemas { get; set; }

    /// <summary>
    /// Number of response status codes that include description fields
    /// Response descriptions help developers understand when and why different status codes occur
    /// </summary>
    [JsonPropertyName("responseCodesDocumented")]
    public int ResponseCodesDocumented { get; set; }

    /// <summary>
    /// Total number of response status codes defined across all endpoints
    /// Used to calculate response documentation coverage percentages
    /// </summary>
    [JsonPropertyName("totalResponseCodes")]
    public int TotalResponseCodes { get; set; }

    /// <summary>
    /// Overall quality score (0-100) based on weighted documentation, examples, and best practices
    /// Combines multiple quality factors into a single, actionable metric for specification improvement
    /// </summary>
    [JsonPropertyName("qualityScore")]
    public double QualityScore { get; set; }
}
