using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;

namespace OpenReferralApi.Core.Services;

/// <summary>
/// Helper class for resolving individual JSON Schema $ref references on-the-fly.
/// Handles both external and internal reference resolution with circular reference detection.
/// Used during custom schema traversal (e.g. additional fields validation).
/// </summary>
public class ReferenceResolver(
    ILogger logger,
    RemoteSchemaLoader remoteSchemaLoader)
{
    private const string CircularReferenceErrorCode = "CIRCULAR_SCHEMA_REFERENCE";
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly RemoteSchemaLoader _remoteSchemaLoader = remoteSchemaLoader ?? throw new ArgumentNullException(nameof(remoteSchemaLoader));
    private readonly Dictionary<string, JsonNode?> _refCache = [];
    private readonly List<SchemaResolutionIssue> _resolutionIssues = [];
    private JsonNode? _rootDocument;
    private string? _baseUri;

    public IReadOnlyList<SchemaResolutionIssue> ResolutionIssues => _resolutionIssues;

    /// <summary>
    /// Initializes the resolver for a new resolution session.
    /// </summary>
    public void Initialize(JsonNode? rootDocument, string? baseUri)
    {
        _refCache.Clear();
        _resolutionIssues.Clear();
        _rootDocument = rootDocument;
        _baseUri = baseUri;
    }

    /// <summary>
    /// Resolves a reference string (internal pointer, anchor, or external URL) on-the-fly to its corresponding JsonNode.
    /// </summary>
    public async Task<JsonNode?> ResolveNodeRefAsync(string refString, JsonNode? rootDocument = null, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        rootDocument ??= _rootDocument;

        if (string.IsNullOrWhiteSpace(refString))
        {
            return null;
        }

        // Check for cyclic reference recursion
        if (visited.Contains(refString))
        {
            _logger.CyclicReferenceDetectedDuringLookup(refString);
            _resolutionIssues.Add(new SchemaResolutionIssue
            {
                ErrorCode = CircularReferenceErrorCode,
                Message = "Circular schema reference detected during dynamic traversal.",
                Reference = refString
            });
            return null;
        }
        visited.Add(refString);

        try
        {
            if (IsInternalRef(refString))
            {
                return await ResolveInternalRefNodeAsync(refString, rootDocument, visited);
            }

            if (IsExternalSchemaRef(refString) || IsLocalSchemaRef(refString))
            {
                var parts = refString.Split('#');
                var schemaUrl = parts[0];
                var fragment = parts.Length > 1 ? $"#{parts[1]}" : string.Empty;

                var schemaLocation = ResolveSchemaLocation(schemaUrl);
                var schema = await LoadSchemaAsync(schemaLocation);

                if (schema == null)
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(fragment))
                {
                    return await ResolveInternalRefNodeAsync(fragment, schema, visited);
                }

                // If no fragment, check if the root itself is a reference
                if (schema is JsonObject obj && obj.TryGetPropertyValue("$ref", out var nestedRef) && nestedRef is JsonValue nv)
                {
                    var nestedRefStr = nv.GetValue<string>();
                    return await ResolveNodeRefAsync(nestedRefStr, schema, visited);
                }

                return schema;
            }

            return null;
        }
        finally
        {
            visited.Remove(refString);
        }
    }

    private async Task<JsonNode?> ResolveInternalRefNodeAsync(string refPointer, JsonNode? doc, HashSet<string> visited)
    {
        if (doc == null)
        {
            return null;
        }

        if (refPointer == "#")
        {
            return doc;
        }

        if (refPointer.StartsWith("#/", StringComparison.Ordinal))
        {
            var pointer = refPointer.TrimStart('#', '/');
            var parts = pointer.Split('/');

            JsonNode? current = doc;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                var unescapedPart = UnescapeJsonPointer(part);

                if (current is JsonObject jsonObj)
                {
                    if (!jsonObj.TryGetPropertyValue(unescapedPart, out current) || current == null)
                    {
                        return null;
                    }
                }
                else if (current is JsonArray jsonArr)
                {
                    if (int.TryParse(unescapedPart, out var index) && index >= 0 && index < jsonArr.Count)
                    {
                        current = jsonArr[index];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            // If the resolved node itself has a $ref, resolve that nested reference
            if (current is JsonObject resolvedObj && resolvedObj.TryGetPropertyValue("$ref", out var nestedRef) && nestedRef is JsonValue nv)
            {
                var nestedRefStr = nv.GetValue<string>();
                return await ResolveNodeRefAsync(nestedRefStr, doc, visited);
            }

            return current;
        }
        else
        {
            // Anchor fragment (e.g. #meta). Supports $anchor and $dynamicAnchor.
            var anchorName = refPointer.TrimStart('#');
            if (string.IsNullOrWhiteSpace(anchorName))
            {
                return null;
            }

            var current = FindAnchorNode(doc, anchorName);
            if (current == null)
            {
                return null;
            }

            if (current is JsonObject resolvedObj && resolvedObj.TryGetPropertyValue("$ref", out var nestedRef) && nestedRef is JsonValue nv)
            {
                var nestedRefStr = nv.GetValue<string>();
                return await ResolveNodeRefAsync(nestedRefStr, doc, visited);
            }

            return current;
        }
    }

    private static bool IsExternalSchemaRef(string refString)
    {
        return refString.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               refString.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalSchemaRef(string refString)
    {
        if (string.IsNullOrWhiteSpace(refString))
        {
            return false;
        }

        var schemaPart = refString.Split('#')[0];
        if (string.IsNullOrWhiteSpace(schemaPart))
        {
            return false;
        }

        if (Uri.TryCreate(schemaPart, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.Scheme == Uri.UriSchemeFile;
        }

        return Path.IsPathRooted(schemaPart) ||
               !schemaPart.Contains("://", StringComparison.Ordinal);
    }

    private static bool IsInternalRef(string refString)
    {
        return refString.StartsWith('#');
    }

    private static JsonNode? FindAnchorNode(JsonNode? node, string anchorName)
    {
        if (node == null)
        {
            return null;
        }

        if (node is JsonObject jsonObject)
        {
            if (HasMatchingAnchor(jsonObject, "$anchor", anchorName) ||
                HasMatchingAnchor(jsonObject, "$dynamicAnchor", anchorName))
            {
                return jsonObject;
            }

            foreach (var kvp in jsonObject)
            {
                var found = FindAnchorNode(kvp.Value, anchorName);
                if (found != null)
                {
                    return found;
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                var found = FindAnchorNode(item, anchorName);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static bool HasMatchingAnchor(JsonObject node, string propertyName, string anchorName)
    {
        if (!node.TryGetPropertyValue(propertyName, out var anchorNode) ||
            anchorNode is not JsonValue anchorValue)
        {
            return false;
        }

        return string.Equals(anchorValue.GetValue<string>(), anchorName, StringComparison.Ordinal);
    }

    private string ResolveSchemaLocation(string schemaRef)
    {
        if (string.IsNullOrWhiteSpace(schemaRef))
        {
            return _baseUri ?? string.Empty;
        }

        if (Uri.TryCreate(schemaRef, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.Scheme == Uri.UriSchemeFile
                ? absoluteUri.LocalPath
                : schemaRef;
        }

        if (string.IsNullOrWhiteSpace(_baseUri))
        {
            return Path.GetFullPath(schemaRef);
        }

        if (Uri.TryCreate(_baseUri, UriKind.Absolute, out var baseUri))
        {
            if (Uri.TryCreate(baseUri, schemaRef, out var resolvedUri))
            {
                return resolvedUri.Scheme == Uri.UriSchemeFile
                    ? resolvedUri.LocalPath
                    : resolvedUri.ToString();
            }
        }

        var basePath = _baseUri;
        if (!string.IsNullOrEmpty(Path.GetExtension(basePath)))
        {
            basePath = Path.GetDirectoryName(basePath) ?? basePath;
        }

        return Path.GetFullPath(Path.Combine(basePath, schemaRef));
    }

    private async Task<JsonNode?> LoadSchemaAsync(string schemaLocation)
    {
        if (Uri.TryCreate(schemaLocation, UriKind.Absolute, out var schemaUri) &&
            (schemaUri.Scheme == Uri.UriSchemeHttp || schemaUri.Scheme == Uri.UriSchemeHttps))
        {
            return await _remoteSchemaLoader.LoadRemoteSchemaAsync(schemaLocation);
        }

        var localPath = schemaLocation;
        if (Uri.TryCreate(schemaLocation, UriKind.Absolute, out var fileUri) &&
            fileUri.Scheme == Uri.UriSchemeFile)
        {
            localPath = fileUri.LocalPath;
        }

        if (!File.Exists(localPath))
        {
            _logger.SchemaFileNotFound(localPath);
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(localPath);
            return JsonNode.Parse(content);
        }
        catch (Exception ex)
        {
            _logger.FailedToLoadLocalSchemaFile(ex, localPath);
            throw;
        }
    }

    private static string UnescapeJsonPointer(string token)
    {
        return token.Replace("~1", "/").Replace("~0", "~");
    }
}
