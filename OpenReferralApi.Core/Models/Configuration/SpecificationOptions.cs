namespace OpenReferralApi.Core.Models.Configuration;

public class SpecificationOptions
{
    public const string SectionName = "Specification";

    /// <summary>
    /// Enables schema warmup on application startup.
    /// </summary>
    public bool WarmupEnabled { get; set; } = true;

    /// <summary>
    /// Delay before warmup starts so app startup is not blocked.
    /// </summary>
    public int WarmupStartupDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Optional configuration-based profile-name to OpenAPI URL mappings.
    /// Values here are merged with the built-in defaults for resolution and are
    /// also used as the source list for schema warmup.
    /// </summary>
    public Dictionary<string, string> Urls { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional default HSDS profile key (for example, "HSDS-UK-1.0") used only
    /// as a fallback when profile version detection or feed OpenAPI retrieval fails.
    /// When provided, it must match one of the configured Urls keys.
    /// </summary>
    public string? DefaultProfileVersion { get; set; }

    /// <summary>
    /// Map of target known profile versions to an array of alias/discovered versions that should map to them.
    /// For example, "HSDS-UK-3.0": ["V3"].
    /// </summary>
    public Dictionary<string, string[]> ProfileVersionMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
