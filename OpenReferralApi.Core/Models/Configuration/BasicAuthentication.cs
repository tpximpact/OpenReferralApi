using System.ComponentModel;
using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Configuration;

/// <summary>
/// Represents basic authentication credentials for HTTP requests
/// </summary>
public class BasicAuthentication
{
    [DefaultValue("")]
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [DefaultValue("")]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}
