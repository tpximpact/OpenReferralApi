using System.ComponentModel;
using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Configuration;

public abstract class ValidationOptionsBase
{
    [DefaultValue(30)]
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [DefaultValue(5)]
    [JsonPropertyName("maxConcurrentRequests")]
    public int MaxConcurrentRequests { get; set; } = 5;

    [JsonPropertyName("reportAdditionalFields")]
    public bool ReportAdditionalFields { get; set; } = false;
}