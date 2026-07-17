using System.ComponentModel;
using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Configuration;

/// <summary>
/// Represents authentication credentials and configuration for accessing the API server during endpoint testing
/// </summary>
public class DataSourceAuthentication : IAuthenticationConfig
{
    [DefaultValue("")]
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [DefaultValue("X-API-Key")]
    [JsonPropertyName("apiKeyHeader")]
    public string ApiKeyHeader { get; set; } = "X-API-Key";

    [DefaultValue("")]
    [JsonPropertyName("bearerToken")]
    public string? BearerToken { get; set; }

    [JsonPropertyName("basicAuth")]
    public BasicAuthentication? BasicAuth { get; set; }

    [JsonPropertyName("customHeaders")]
    public Dictionary<string, string>? CustomHeaders { get; set; } = [];
}
