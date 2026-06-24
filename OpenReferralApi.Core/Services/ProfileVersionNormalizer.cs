namespace OpenReferralApi.Core.Services;

internal static class ProfileVersionNormalizer
{
    /// <summary>
    /// Extracts the trailing major.minor version number from a raw profile version string.
    /// For example "HSDS-UK-3.0" → "3.0", "V3" → "3.0", "3.2" → "3.2", "SOMESCHEMA-1.5" → "1.5".
    /// Returns null if no version number can be extracted.
    /// </summary>
    internal static string? ExtractMajorMinor(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return null;
        }

        var span = rawVersion.AsSpan().Trim();
        if (span.IsEmpty)
        {
            return null;
        }

        int i = span.Length - 1;

        // Find trailing digits (minor or major)
        int end1 = i;
        while (i >= 0 && char.IsAsciiDigit(span[i]))
        {
            i--;
        }

        if (i == end1)
        {
            // No trailing digits
            return null;
        }

        int start1 = i + 1;

        if (i >= 0 && span[i] == '.')
        {
            // We found a dot, so the first block might be minor. Look for major.
            i--;
            int end2 = i;
            while (i >= 0 && char.IsAsciiDigit(span[i]))
            {
                i--;
            }

            if (i != end2)
            {
                var major = span.Slice(i + 1, end2 - i);
                var minor = span.Slice(start1, end1 - start1 + 1);
                return $"{major}.{minor}";
            }
        }

        // No dot, or dot not preceded by digits. Treat trailing digits as major.
        var majorOnly = span.Slice(start1, end1 - start1 + 1);
        return $"{majorOnly}.0";
    }

    /// <summary>
    /// Kept for backward compatibility with call sites that have not yet been migrated.
    /// Prefer ExtractMajorMinor for new code.
    /// </summary>
    internal static string? NormalizeVersionNumber(string? rawVersion) => ExtractMajorMinor(rawVersion);
}
