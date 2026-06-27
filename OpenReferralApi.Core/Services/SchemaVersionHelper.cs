using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using OpenReferralApi.Core.Models.Configuration;

namespace OpenReferralApi.Core.Services;

internal static partial class SchemaVersionHelper
{
    // Centralized compiled regex for extracting profile version from specification URLs
    [GeneratedRegex(@"/specifications/(?<version>[^/]+)/openapi\.json", RegexOptions.IgnoreCase)]
    private static partial Regex SchemaUrlVersionRegex();

    /// <summary>
    /// Extracts the HSDS profile version from the schema URL using the centralized regex pattern.
    /// </summary>
    public static string? TryExtractProfileVersionFromSchemaUrl(string? schemaUrl)
    {
        if (string.IsNullOrWhiteSpace(schemaUrl))
        {
            return null;
        }

        var match = SchemaUrlVersionRegex().Match(schemaUrl);
        if (!match.Success)
        {
            return null;
        }

        var extracted = match.Groups["version"].Value.Trim();
        return string.IsNullOrWhiteSpace(extracted) ? null : extracted;
    }

    /// <summary>
    /// Resolves a profile version to its configured baseline schema URL, with direct match and major-minor normalizer fallback.
    /// </summary>
    public static bool TryGetSchemaUrlForProfileVersion(
        string? profileVersion, 
        SpecificationOptions? options, 
        [NotNullWhen(true)] out string? schemaUrl)
    {
        schemaUrl = null;
        if (string.IsNullOrWhiteSpace(profileVersion) || options?.Urls == null || options.Urls.Count == 0)
        {
            return false;
        }

        var trimmed = profileVersion.Trim();

        // 1. Direct match (case-insensitive)
        foreach (var entry in options.Urls)
        {
            if (string.Equals(entry.Key, trimmed, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(entry.Value))
            {
                schemaUrl = entry.Value.Trim();
                return true;
            }
        }

        // 2. Numeric major.minor fallback using ProfileVersionNormalizer
        var requestedNumeric = ProfileVersionNormalizer.ExtractMajorMinor(trimmed);
        if (!string.IsNullOrWhiteSpace(requestedNumeric))
        {
            foreach (var entry in options.Urls)
            {
                var keyNumeric = ProfileVersionNormalizer.ExtractMajorMinor(entry.Key);
                if (string.Equals(keyNumeric, requestedNumeric, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    schemaUrl = entry.Value.Trim();
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves a profile key from a version number (case-insensitive normalization).
    /// </summary>
    public static string? TryResolveConfiguredProfileKeyFromVersion(string? versionNumber, SpecificationOptions? options)
    {
        if (string.IsNullOrWhiteSpace(versionNumber) || options?.Urls == null || options.Urls.Count == 0)
        {
            return null;
        }

        var normalizedInput = ProfileVersionNormalizer.NormalizeVersionNumber(versionNumber);

        return options.Urls.Keys
            .FirstOrDefault(key => string.Equals(
                ProfileVersionNormalizer.NormalizeVersionNumber(key),
                normalizedInput,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves a profile key from a schema URL.
    /// </summary>
    public static string? TryResolveConfiguredProfileKeyFromSchemaUrl(string? schemaUrl, SpecificationOptions? options)
    {
        if (string.IsNullOrWhiteSpace(schemaUrl) || options?.Urls == null || options.Urls.Count == 0)
        {
            return null;
        }

        if (!Uri.TryCreate(schemaUrl, UriKind.Absolute, out var requestedUri))
        {
            return null;
        }

        var requestedAbsoluteUrl = requestedUri.AbsoluteUri.TrimEnd('/');

        foreach (var configuredEntry in options.Urls)
        {
            if (string.IsNullOrWhiteSpace(configuredEntry.Value)
                || !Uri.TryCreate(configuredEntry.Value, UriKind.Absolute, out var configuredUri))
            {
                continue;
            }

            if (string.Equals(configuredUri.AbsoluteUri.TrimEnd('/'), requestedAbsoluteUrl, StringComparison.OrdinalIgnoreCase))
            {
                return configuredEntry.Key;
            }
        }

        return null;
    }
}
