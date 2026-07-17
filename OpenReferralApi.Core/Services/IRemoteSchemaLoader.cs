#nullable enable
using System.Text.Json.Nodes;
using OpenReferralApi.Core.Models.Configuration;

namespace OpenReferralApi.Core.Services;

/// <summary>
/// Defines the contract for loading remote JSON schemas with caching and authentication support.
/// </summary>
public interface IRemoteSchemaLoader
{
    /// <summary>
    /// Sets the authentication configuration for this loader instance.
    /// </summary>
    void SetAuthentication(IAuthenticationConfig? auth);

    /// <summary>
    /// Asynchronously loads a remote schema from a URL.
    /// </summary>
    Task<JsonNode?> LoadRemoteSchemaAsync(string schemaUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously loads a remote schema from a URL.
    /// </summary>
    JsonNode? LoadRemoteSchema(string schemaUrl);
}
