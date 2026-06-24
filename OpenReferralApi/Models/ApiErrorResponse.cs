namespace OpenReferralApi.Models;

/// <summary>
/// Standard API error payload used for documented non-2xx controller responses.
/// </summary>
internal sealed class ApiErrorResponse
{
    /// <summary>
    /// High-level error identifier.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Detailed error message when available.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Optional feed identifier associated with the error.
    /// </summary>
    public string? FeedId { get; set; }

    /// <summary>
    /// Optional file path associated with the error.
    /// </summary>
    public string? File { get; set; }
}
