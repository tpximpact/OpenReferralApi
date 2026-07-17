using System.Text.RegularExpressions;

namespace OpenReferralApi.Core.Helpers;

public static partial class TextSanitizer
{
    [GeneratedRegex(@"\p{Cc}+")]
    private static partial Regex ControlCharsRegex();

    [GeneratedRegex(@"[^\x20-\x7E]+")]
    private static partial Regex NonPrintableAsciiRegex();

    [GeneratedRegex(@" +")]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9\-._~:/?#\[\]@!$&'()*+,;=%]")]
    private static partial Regex NonUrlSafeRegex();

    public static string SanitizeExceptionMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        var sanitized = ControlCharsRegex().Replace(message, string.Empty);

        const int maxLength = 500;
        if (sanitized.Length > maxLength)
        {
            return string.Concat(sanitized.AsSpan(0, maxLength), "...(truncated)");
        }

        return sanitized;
    }

    public static string SanitizeForLogging(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
    }

    public static string SanitizeStringForLogging(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sanitized = NonPrintableAsciiRegex().Replace(input, string.Empty);
        sanitized = MultipleSpacesRegex().Replace(sanitized, " ").Trim();

        const int maxLength = 500;
        if (sanitized.Length > maxLength)
        {
            sanitized = string.Concat(sanitized.AsSpan(0, maxLength), "...(truncated)");
        }

        sanitized = sanitized.Replace("{", "{{").Replace("}", "}}");
        return string.Concat("[user: ", sanitized, "]");
    }

    public static string SanitizeUrlForLogging(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        var noControls = ControlCharsRegex().Replace(url.Trim(), string.Empty);
        var cleaned = NonUrlSafeRegex().Replace(noControls, "?");

        const int maxLength = 2048;
        if (cleaned.Length > maxLength)
        {
            cleaned = string.Concat(cleaned.AsSpan(0, maxLength), "...(truncated)");
        }

        try
        {
            if (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri))
            {
                return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
            }

            var questionMarkIndex = cleaned.IndexOf('?');
            var hashIndex = cleaned.IndexOf('#');
            var endIndex = cleaned.Length;

            if (questionMarkIndex > 0)
                endIndex = Math.Min(endIndex, questionMarkIndex);
            if (hashIndex > 0)
                endIndex = Math.Min(endIndex, hashIndex);

            return cleaned[..endIndex];
        }
        catch
        {
            const int fallbackMaxLength = 100;
            if (cleaned.Length > fallbackMaxLength)
            {
                return string.Concat(cleaned.AsSpan(0, fallbackMaxLength), "...");
            }
            return cleaned;
        }
    }
}
