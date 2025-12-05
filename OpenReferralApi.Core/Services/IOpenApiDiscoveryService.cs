namespace OpenReferralApi.Core.Services;

public interface IOpenApiDiscoveryService
{
    /// <summary>
    /// Attempts to discover an OpenAPI schema URL from the provided base URL.
    /// Returns the discovered URL or null if none found.
    /// </summary>
    Task<string?> DiscoverOpenApiUrlAsync(string baseUrl, CancellationToken cancellationToken = default);
}
