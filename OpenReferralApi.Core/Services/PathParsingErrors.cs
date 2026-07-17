using System;
using System.Text;

namespace OpenReferralApi.Core.Services;

public static class PathParsingErrors
{
    private static readonly CompositeFormat RelativeUriNullOrEmptyFormat =
        CompositeFormat.Parse("Relative URI cannot be null or empty");

    private static readonly CompositeFormat ResolveRelativeUriFailedFormat =
        CompositeFormat.Parse("Failed to resolve relative URI '{0}' against base '{1}': {2}");

    private static readonly CompositeFormat UriNullOrEmptyFormat =
        CompositeFormat.Parse("{0} cannot be null or empty");

    private static readonly CompositeFormat InvalidUriFormat =
        CompositeFormat.Parse("Invalid {0}: {1}");

    private static readonly CompositeFormat UnsupportedSchemeFormat =
        CompositeFormat.Parse("{0} scheme '{1}' is not supported. Allowed schemes: {2}");

    private static readonly CompositeFormat InvalidUriFormatStringFormat =
        CompositeFormat.Parse("Invalid {0} format: {1}");

    private static readonly CompositeFormat DisallowedPortFormat =
        CompositeFormat.Parse("{0} uses disallowed port: {1}");

    public static ArgumentException RelativeUriNullOrEmpty(string paramName) =>
        new(string.Format(null, RelativeUriNullOrEmptyFormat), paramName);

    public static ArgumentException ResolveRelativeUriFailed(string? relativeUri, Uri? baseUrl, string? message, Exception innerException) =>
        new(string.Format(null, ResolveRelativeUriFailedFormat, relativeUri ?? string.Empty, baseUrl?.ToString() ?? string.Empty, message ?? string.Empty), innerException);

    public static ArgumentException UriNullOrEmpty(string? uriType, string paramName) =>
        new(string.Format(null, UriNullOrEmptyFormat, uriType ?? string.Empty), paramName);

    public static ArgumentException InvalidUri(string? uriType, string? uriString) =>
        new(string.Format(null, InvalidUriFormat, uriType ?? string.Empty, uriString ?? string.Empty));

    public static ArgumentException UnsupportedScheme(string? uriType, string? scheme, string allowedSchemes) =>
        new(string.Format(null, UnsupportedSchemeFormat, uriType ?? string.Empty, scheme ?? string.Empty, allowedSchemes));

    public static ArgumentException InvalidUriFormatString(string? uriType, string? uriString, Exception innerException) =>
        new(string.Format(null, InvalidUriFormatStringFormat, uriType ?? string.Empty, uriString ?? string.Empty), innerException);

    public static ArgumentException DisallowedPort(string? uriType, int port) =>
        new(string.Format(null, DisallowedPortFormat, uriType ?? string.Empty, port));
}
