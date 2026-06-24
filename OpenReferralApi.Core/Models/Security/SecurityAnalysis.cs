using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Security;

/// <summary>
/// Comprehensive analysis of the API's security configuration, schemes, and vulnerabilities
/// </summary>
public class SecurityAnalysis
{
    /// <summary>
    /// Total number of security schemes defined in the specification
    /// Indicates the variety of authentication methods supported by the API
    /// </summary>
    [JsonPropertyName("securitySchemesCount")]
    public int SecuritySchemesCount { get; set; }

    /// <summary>
    /// Detailed information about each security scheme configured in the specification
    /// Provides insight into authentication methods, their security levels, and configurations
    /// </summary>
    [JsonPropertyName("securitySchemes")]
    public List<SecuritySchemeInfo> SecuritySchemes { get; set; } = [];

    /// <summary>
    /// List of security requirements that apply globally to all endpoints
    /// Shows which authentication methods are required by default across the API
    /// </summary>
    [JsonPropertyName("globalSecurityRequirements")]
    public List<string> GlobalSecurityRequirements { get; set; } = [];

    /// <summary>
    /// Number of endpoints that have security requirements (either global or operation-specific)
    /// Higher numbers indicate better security coverage across the API
    /// </summary>
    [JsonPropertyName("endpointsWithSecurity")]
    public int EndpointsWithSecurity { get; set; }

    /// <summary>
    /// Number of endpoints that lack any security requirements
    /// These endpoints are publicly accessible and may represent security risks
    /// </summary>
    [JsonPropertyName("endpointsWithoutSecurity")]
    public int EndpointsWithoutSecurity { get; set; }

    /// <summary>
    /// List of security-related recommendations for improving the API's security posture
    /// Includes suggestions for authentication improvements, vulnerability mitigation, and best practices
    /// </summary>
    [JsonPropertyName("securityRecommendations")]
    public List<string> SecurityRecommendations { get; set; } = [];
}
