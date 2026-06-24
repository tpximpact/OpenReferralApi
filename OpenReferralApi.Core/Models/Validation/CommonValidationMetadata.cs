using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Validation;

/// <summary>
/// Consolidated metadata model shared across JSON schema and OpenAPI validation flows.
/// </summary>
public class CommonValidationMetadata : IMetadata
{
    [JsonPropertyName("openApiVersion")]
    public string? OpenApiVersion { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    [JsonPropertyName("testTimestamp")]
    public DateTime? TestTimestamp { get; set; }

    [JsonPropertyName("testDuration")]
    public TimeSpan? TestDuration { get; set; }

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    internal string? ProfileReason { get; set; }

    [JsonPropertyName("schemaTitle")]
    public string? SchemaTitle { get; set; }

    [JsonPropertyName("schemaDescription")]
    public string? SchemaDescription { get; set; }

    [JsonPropertyName("dataSize")]
    public long? DataSize { get; set; }

    [JsonPropertyName("validationTimestamp")]
    public DateTime? ValidationTimestamp { get; set; }

    [JsonPropertyName("dataSource")]
    public string? DataSource { get; set; }

    [JsonIgnore]
    public DateTime Timestamp
    {
        get => TestTimestamp ?? ValidationTimestamp ?? DateTime.UtcNow;
        set
        {
            if (TestTimestamp.HasValue || !ValidationTimestamp.HasValue)
            {
                TestTimestamp = value;
                return;
            }

            ValidationTimestamp = value;
        }
    }
}