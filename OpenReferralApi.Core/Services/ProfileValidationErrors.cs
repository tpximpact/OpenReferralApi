using System;
using System.Text;

namespace OpenReferralApi.Core.Services;

public static class ProfileValidationErrors
{
    private static readonly CompositeFormat NoProfileDiscoveredAndNoDefaultFormat =
        CompositeFormat.Parse("Can only validate against known profile versions. No HSDS profile version was discovered from the base URL and no default profile is configured.");

    private static readonly CompositeFormat DiscoveredAndDefaultUnsupportedFormat =
        CompositeFormat.Parse("Can only validate against known profile versions. Discovered profile '{0}' is not supported and the default profile '{1}' is also not supported.");

    private static readonly CompositeFormat DiscoveredUnsupportedFormat =
        CompositeFormat.Parse("Can only validate against known profile versions. Discovered profile '{0}' is not supported.");

    private static readonly CompositeFormat ProfileSchemaNotCachedFormat =
        CompositeFormat.Parse("Can only validate against known profile versions. Schema for profile '{0}' is not available in cache.");

    public static ArgumentException NoProfileDiscoveredAndNoDefault() =>
        new(string.Format(null, NoProfileDiscoveredAndNoDefaultFormat));

    public static ArgumentException DiscoveredAndDefaultUnsupported(string? discoveredVersion, string? defaultProfileVersion) =>
        new(string.Format(null, DiscoveredAndDefaultUnsupportedFormat, discoveredVersion ?? string.Empty, defaultProfileVersion ?? string.Empty));

    public static ArgumentException DiscoveredUnsupported(string? discoveredVersion) =>
        new(string.Format(null, DiscoveredUnsupportedFormat, discoveredVersion ?? string.Empty));

    public static ArgumentException ProfileSchemaNotCached(string? discoveredVersion) =>
        new(string.Format(null, ProfileSchemaNotCachedFormat, discoveredVersion ?? string.Empty));
}
