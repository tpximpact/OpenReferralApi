using System;
using Newtonsoft.Json;

namespace OpenReferralApi.Core.Models;

public class ValidationMetadata : IMetadata
{
    [JsonProperty("schemaTitle")]
    public string? SchemaTitle { get; set; }

    [JsonProperty("schemaDescription")]
    public string? SchemaDescription { get; set; }

    [JsonProperty("dataSize")]
    public long DataSize { get; set; }

    [JsonProperty("validationTimestamp")]
    public DateTime ValidationTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Implements IMetadata.Timestamp
    /// </summary>
    [JsonIgnore]
    public DateTime Timestamp
    {
        get => ValidationTimestamp;
        set => ValidationTimestamp = value;
    }

    [JsonProperty("dataSource")]
    public string? DataSource { get; set; }
}
