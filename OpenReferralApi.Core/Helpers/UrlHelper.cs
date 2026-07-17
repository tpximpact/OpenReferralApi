using System;

namespace OpenReferralApi.Core.Helpers;

public static class UrlHelper
{
    public static string? NormalizeAbsoluteUrl(string? schemaUrl)
    {
        if (string.IsNullOrWhiteSpace(schemaUrl))
        {
            return null;
        }

        if (schemaUrl.Contains("json-everything.lib", StringComparison.OrdinalIgnoreCase))
        {
            schemaUrl = schemaUrl.Replace("json-everything.lib", "json-everything.net", StringComparison.OrdinalIgnoreCase);
        }

        if (!Uri.TryCreate(schemaUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
