using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using Json.Pointer;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;
using ValidationError = OpenReferralApi.Core.Models.Validation.ValidationError;

namespace OpenReferralApi.Core.Services;

public interface IJsonValidatorService
{
    Task<ValidationResult> ValidateAsync(ValidationRequest request, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateWithSchemaUriAsync(object jsonData, string schemaUri, ValidationOptions? options = null, CancellationToken cancellationToken = default);
    Task<bool> IsValidAsync(ValidationRequest request, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateSchemaAsync(object schema, CancellationToken cancellationToken = default);
}

public class JsonValidatorService : IJsonValidatorService
{
    private const int MaxAllowedJsonDepth = 64;
    private static readonly ConcurrentDictionary<string, CachedExternalSchemaDocument> ExternalSchemaUriCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, ResolvedSchemaDetails> CompiledSchemaCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string ArrayIndexToken = "[]";
    private static readonly System.Text.Json.JsonSerializerOptions DefaultSerializerOptions = new()
    {
        MaxDepth = MaxAllowedJsonDepth
    };

    private readonly ILogger<JsonValidatorService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPathParsingService _pathParsingService;
    private readonly IRequestProcessingService _requestProcessingService;
    private readonly ISchemaResolverService _schemaResolverService;
    private readonly bool _externalSchemaUriCacheEnabled;
    private readonly TimeSpan _externalSchemaUriCacheTtl;

    public JsonValidatorService(
        ILogger<JsonValidatorService> logger,
        IHttpClientFactory httpClientFactory,
        IPathParsingService pathParsingService,
        IRequestProcessingService requestProcessingService,
        ISchemaResolverService schemaResolverService,
        IOptions<CacheOptions>? cacheOptions = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _pathParsingService = pathParsingService;
        _requestProcessingService = requestProcessingService;
        _schemaResolverService = schemaResolverService;

        var effectiveCacheOptions = cacheOptions?.Value;
        _externalSchemaUriCacheEnabled = effectiveCacheOptions?.Enabled ?? true;
        _externalSchemaUriCacheTtl = effectiveCacheOptions != null && effectiveCacheOptions.ExpirationMinutes > 0
            ? TimeSpan.FromMinutes(effectiveCacheOptions.ExpirationMinutes)
            : TimeSpan.FromHours(2);
    }

    public async Task<ValidationResult> ValidateAsync(ValidationRequest request, CancellationToken cancellationToken = default)
    {
        return await _requestProcessingService.ExecuteWithConcurrencyControlAsync(
            ct => ValidateCoreAsync(request, ct),
            request.Options,
            cancellationToken);
    }

    public async Task<ValidationResult> ValidateWithSchemaUriAsync(object jsonData, string schemaUri, ValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new ValidationRequest
        {
            JsonData = jsonData,
            SchemaUri = schemaUri,
            Options = options
        };

        return await _requestProcessingService.ExecuteWithConcurrencyControlAsync(
            ct => ValidateCoreAsync(request, ct),
            options,
            cancellationToken);
    }

    public async Task<bool> IsValidAsync(ValidationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _requestProcessingService.ExecuteWithConcurrencyControlAsync(
            ct => ValidateCoreAsync(request, ct),
            request.Options,
            cancellationToken);
        return result.IsValid;
    }

    private async Task<ValidationResult> ValidateCoreAsync(ValidationRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ValidationResult();

        try
        {
            _logger.StartingJsonValidation();

            // Create timeout token
            using var timeoutCts = _requestProcessingService.CreateTimeoutToken(request.Options, cancellationToken);
            var effectiveToken = timeoutCts.Token;

            // Get JSON data and schema concurrently if possible
            var dataTask = GetJsonDataAsync(request, effectiveToken);
            var schemaTask = GetSchemaAsync(request, effectiveToken);

            var jsonDataDoc = await dataTask;
            using var ownedJsonDataDoc = request.JsonData is not System.Text.Json.JsonDocument
                ? jsonDataDoc
                : null;
            var schema = await schemaTask;

            // Fail fast: check for required root properties (example: "type")
            var requiredProperties = GetRequiredRootProperties(schema.SchemaNode);
            if (requiredProperties.Count > 0)
            {
                foreach (var requiredProp in requiredProperties)
                {
                    if (!jsonDataDoc.RootElement.TryGetProperty(requiredProp, out _))
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Path = requiredProp,
                            Message = $"Missing required property: {requiredProp}",
                            ErrorCode = "MISSING_REQUIRED_PROPERTY",
                            Severity = "Error"
                        });
                        // Fail fast: stop further validation
                        return result;
                    }
                }
            }

            var dataSize = GetJsonDocumentUtf8Size(jsonDataDoc);

            // Selective parsing: only validate properties present in schema
            var validationErrors = await ValidateJsonAgainstSchemaAsync(jsonDataDoc, schema, request.Options);

            // Report additional fields if requested
            if (request.Options?.ReportAdditionalFields == true)
            {
                var additionalFieldWarnings = DetectAdditionalFields(jsonDataDoc.RootElement, schema.SchemaNode);
                validationErrors.AddRange(additionalFieldWarnings);
            }

            // Build result - only count Error severity as validation failures
            result.IsValid = !validationErrors.Any(e => e.Severity == "Error");
            result.Errors = validationErrors;
            result.SchemaVersion = "2020-12";
            result.Metadata = new CommonValidationMetadata
            {
                SchemaTitle = GetSchemaTitle(request, schema),
                SchemaDescription = GetSchemaDescription(request, schema),
                DataSize = dataSize,
                ValidationTimestamp = DateTime.UtcNow,
                DataSource = !string.IsNullOrEmpty(request.DataUrl) ? request.DataUrl : "direct"
            };

            _logger.JsonValidationCompleted(result.IsValid, result.Errors.Count);
        }
        catch (JsonStructureViolationException ex)
        {
            result.IsValid = false;
            result.Errors.Add(MapJsonStructureViolationToValidationError(ex));
        }
        catch (ArgumentException ex)
        {
            _logger.InvalidArgumentDuringJsonValidation(ex);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.InvalidOperationDuringJsonValidation(ex);
            throw;
        }
        catch (Exception ex)
        {
            _logger.UnexpectedErrorDuringJsonValidation(ex);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Path = "",
                Message = $"Validation failed: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}",
                ErrorCode = "VALIDATION_ERROR",
                Severity = "Error"
            });
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<ValidationResult> ValidateSchemaAsync(object schema, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ValidationResult();

        try
        {
            _logger.StartingSchemaValidation();

            var schemaJson = System.Text.Json.JsonSerializer.Serialize(schema);
            _ = await _schemaResolverService.CreateSchemaFromJsonAsync(schemaJson, cancellationToken);
            var resolvedSchemaJson = await _schemaResolverService.ResolveAsync(schemaJson);
            var effectiveSchemaJson = string.IsNullOrWhiteSpace(resolvedSchemaJson) ? schemaJson : resolvedSchemaJson;
            var schemaDetails = BuildSchemaDetails(effectiveSchemaJson);

            // Basic schema validation
            var schemaValidationErrors = new List<ValidationError>();

            if (!HasRootTypeKeyword(schemaDetails.SchemaNode))
            {
                schemaValidationErrors.Add(new ValidationError
                {
                    Path = "",
                    Message = "Schema should specify a type",
                    ErrorCode = "MISSING_TYPE",
                    Severity = "Warning"
                });
            }

            result.IsValid = schemaValidationErrors.Count == 0;
            result.Errors = schemaValidationErrors;
            result.SchemaVersion = "2020-12";
            result.Metadata = new CommonValidationMetadata
            {
                SchemaTitle = GetSchemaTitleFromObject(schema) ?? schemaDetails.Title,
                SchemaDescription = GetSchemaDescriptionFromObject(schema) ?? schemaDetails.Description,
                ValidationTimestamp = DateTime.UtcNow
            };

            _logger.SchemaValidationCompleted(result.IsValid);
        }
        catch (Exception ex)
        {
            _logger.ErrorDuringSchemaValidation(ex);
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Path = "",
                Message = $"Schema validation failed: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}",
                ErrorCode = "SCHEMA_VALIDATION_ERROR",
                Severity = "Error"
            });
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private async Task<System.Text.Json.JsonDocument> GetJsonDataAsync(ValidationRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.DataUrl))
        {
            return await FetchJsonDataFromUrlAsync(request.DataUrl, request.Options, cancellationToken);
        }
        else if (request.JsonData is string jsonString)
        {
            try
            {
                return System.Text.Json.JsonDocument.Parse(jsonString);
            }
            catch (System.Text.Json.JsonException ex) when (IsCycleOrDepthViolation(ex))
            {
                const string sourceIdentifier = "request.jsonData (string)";
                _logger.UserJsonCycleOrDepthLimitExceeded(ex, MaxAllowedJsonDepth, sourceIdentifier);
                throw new JsonStructureViolationException(JsonStructureViolationSource.UserProvidedJson, sourceIdentifier, ex);
            }
        }
        else if (request.JsonData is byte[] jsonBytes)
        {
            try
            {
                return System.Text.Json.JsonDocument.Parse(jsonBytes.AsMemory());
            }
            catch (System.Text.Json.JsonException ex) when (IsCycleOrDepthViolation(ex))
            {
                const string sourceIdentifier = "request.jsonData (byte[])";
                _logger.UserJsonCycleOrDepthLimitExceeded(ex, MaxAllowedJsonDepth, sourceIdentifier);
                throw new JsonStructureViolationException(JsonStructureViolationSource.UserProvidedJson, sourceIdentifier, ex);
            }
        }
        else if (request.JsonData is System.Text.Json.JsonDocument doc)
        {
            return doc;
        }
        else if (request.JsonData is System.Text.Json.Nodes.JsonNode jsonNode)
        {
            // JsonNode maintains parent links, so serializing it as a plain object can trigger
            // false cycle detection; parse from its JSON representation instead.
            return System.Text.Json.JsonDocument.Parse(jsonNode.ToJsonString());
        }
        else if (request.JsonData != null)
        {
            try
            {
                return System.Text.Json.JsonSerializer.SerializeToDocument(request.JsonData, DefaultSerializerOptions);
            }
            catch (System.Text.Json.JsonException ex) when (IsCycleOrDepthViolation(ex))
            {
                const string sourceIdentifier = "request.jsonData (object)";
                _logger.UserJsonCycleOrDepthLimitExceeded(ex, MaxAllowedJsonDepth, sourceIdentifier);
                var detectedCyclePath = TryFindCyclePath(request.JsonData);
                throw new JsonStructureViolationException(JsonStructureViolationSource.UserProvidedJson, sourceIdentifier, ex, detectedCyclePath);
            }
        }
        else
        {
            throw new ArgumentException("Either JsonData or DataUrl must be provided");
        }
    }

    private async Task<ResolvedSchemaDetails> GetSchemaAsync(ValidationRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.SchemaUri))
        {
            return await LoadSchemaFromUriAsync(request.SchemaUri, request.Options, cancellationToken);
        }
        else if (request.Schema is JsonSchema compiledSchema)
        {
            return BuildSchemaDetails(System.Text.Json.JsonSerializer.Serialize(compiledSchema));
        }
        else if (request.Schema is string schemaString)
        {
            return await CreateSchemaFromJsonAsync(schemaString);
        }
        else if (request.Schema != null)
        {
            return await CreateSchemaFromObjectAsync(request.Schema);
        }
        else
        {
            throw new ArgumentException("Either Schema, SchemaUri, or SchemaId must be provided");
        }
    }

    private async Task<ResolvedSchemaDetails> LoadSchemaFromUriAsync(string schemaUri, ValidationOptions? options, CancellationToken cancellationToken)
    {
        Uri validatedUri;
        try
        {
            validatedUri = await _pathParsingService.ValidateAndParseSchemaUriAsync(schemaUri, options);
        }
        catch (Exception ex)
        {
            _logger.FailedToLoadSchemaFromUri(ex, schemaUri);
            throw new InvalidOperationException($"Failed to load schema from URI: {schemaUri}", ex);
        }

        var normalizedSchemaUri = validatedUri.ToString();

        // Check if the schema is already registered in SchemaRegistry.Global
        JsonSchemaBuild.EnsureInitialized();
        if (Uri.TryCreate(normalizedSchemaUri, UriKind.Absolute, out var schemaUriObj))
        {
            if (Json.Schema.SchemaRegistry.Global.Get(schemaUriObj) is JsonSchema registered)
            {
                _logger.UsingPreRegisteredSchema(normalizedSchemaUri);
                System.Text.Json.Nodes.JsonNode schemaNode;
                try
                {
                    schemaNode = System.Text.Json.JsonSerializer.SerializeToNode(registered) ?? new System.Text.Json.Nodes.JsonObject();
                }
                catch
                {
                    schemaNode = new System.Text.Json.Nodes.JsonObject();
                }
                var title = TryReadSchemaStringField(schemaNode, "title");
                var description = TryReadSchemaStringField(schemaNode, "description");
                return new ResolvedSchemaDetails(registered, schemaNode, title, description);
            }
        }

        if (TryGetCachedSchemaJson(normalizedSchemaUri, out var cachedSchemaJson))
        {
            _logger.UsingCachedSchemaDocument(normalizedSchemaUri);
            try
            {
                var resolvedSchemaJson = await _schemaResolverService.ResolveAsync(cachedSchemaJson, normalizedSchemaUri, null);
                var effectiveSchemaJson = string.IsNullOrWhiteSpace(resolvedSchemaJson) ? cachedSchemaJson : resolvedSchemaJson;
                return BuildSchemaDetails(effectiveSchemaJson);
            }
            catch (System.Text.Json.JsonException ex) when (IsCycleOrDepthViolation(ex))
            {
                _logger.CachedSchemaCycleOrDepthLimitExceeded(ex, normalizedSchemaUri, MaxAllowedJsonDepth);
                throw new JsonStructureViolationException(JsonStructureViolationSource.CachedSchema, normalizedSchemaUri, ex);
            }
        }

        var retryResult = await _requestProcessingService.ExecuteWithRetryAsync(async (ct) =>
        {
            try
            {
                _logger.LoadingSchemaFromUri(normalizedSchemaUri);

                var httpClient = _httpClientFactory.CreateClient();
                using var response = await httpClient.GetAsync(validatedUri, ct);
                _ = response.EnsureSuccessStatusCode();
                var schemaJson = await response.Content.ReadAsStringAsync(ct);

                if (_externalSchemaUriCacheEnabled)
                {
                    PurgeExpiredExternalSchemaEntries();
                    ExternalSchemaUriCache[normalizedSchemaUri] = new CachedExternalSchemaDocument(
                        schemaJson,
                        DateTime.UtcNow.Add(_externalSchemaUriCacheTtl));
                }

                var resolvedSchemaJson = await _schemaResolverService.ResolveAsync(schemaJson, normalizedSchemaUri, null);
                var effectiveSchemaJson = string.IsNullOrWhiteSpace(resolvedSchemaJson) ? schemaJson : resolvedSchemaJson;
                return (object)BuildSchemaDetails(effectiveSchemaJson);
            }
            catch (Exception ex)
            {
                _logger.FailedToLoadSchemaFromUriRetry(ex, normalizedSchemaUri);
                throw new InvalidOperationException($"Failed to load schema from URI: {normalizedSchemaUri}", ex);
            }
        }, options, cancellationToken);

        return (ResolvedSchemaDetails)retryResult;
    }

    private bool TryGetCachedSchemaJson(string schemaUri, out string schemaJson)
    {
        schemaJson = string.Empty;

        if (!_externalSchemaUriCacheEnabled)
        {
            return false;
        }

        if (!ExternalSchemaUriCache.TryGetValue(schemaUri, out var cachedEntry))
        {
            return false;
        }

        if (cachedEntry.ExpiresAtUtc <= DateTime.UtcNow || string.IsNullOrWhiteSpace(cachedEntry.SchemaJson))
        {
            _ = ExternalSchemaUriCache.TryRemove(schemaUri, out _);
            return false;
        }

        schemaJson = cachedEntry.SchemaJson;
        return true;
    }

    private sealed record CachedExternalSchemaDocument(string SchemaJson, DateTime ExpiresAtUtc);

    private static void PurgeExpiredExternalSchemaEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var key in ExternalSchemaUriCache.Keys.ToList())
        {
            if (ExternalSchemaUriCache.TryGetValue(key, out var entry) && entry.ExpiresAtUtc <= now)
            {
                _ = ExternalSchemaUriCache.TryRemove(key, out _);
            }
        }
    }

    private async Task<ResolvedSchemaDetails> CreateSchemaFromObjectAsync(object schema)
    {
        try
        {
            var schemaNode = schema switch
            {
                System.Text.Json.Nodes.JsonNode node => node.DeepClone(),
                System.Text.Json.JsonDocument doc => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(doc, DefaultSerializerOptions),
                System.Text.Json.JsonElement element => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(element, DefaultSerializerOptions),
                _ => System.Text.Json.JsonSerializer.SerializeToNode(schema, DefaultSerializerOptions)
            } ?? throw new InvalidOperationException("Failed to serialize schema object to JsonNode.");
            return await CreateSchemaFromNodeAsync(schemaNode);
        }
        catch (System.Text.Json.JsonException ex) when (IsCycleOrDepthViolation(ex))
        {
            const string sourceIdentifier = "request.schema";
            _logger.UserSchemaCycleOrDepthLimitExceeded(ex, MaxAllowedJsonDepth, sourceIdentifier);
            var detectedCyclePath = TryFindCyclePath(schema);
            throw new JsonStructureViolationException(JsonStructureViolationSource.UserProvidedSchema, sourceIdentifier, ex, detectedCyclePath);
        }
        catch (Exception ex)
        {
            _logger.FailedToCreateSchemaFromObject(ex);
            throw new InvalidOperationException("Failed to create schema from object", ex);
        }
    }

    private async Task<ResolvedSchemaDetails> CreateSchemaFromNodeAsync(System.Text.Json.Nodes.JsonNode schemaNode, string? documentUri = null)
    {
        var resolvedSchemaNode = await _schemaResolverService.ResolveAsync(schemaNode, documentUri, auth: null);
        var effectiveSchemaNode = resolvedSchemaNode ?? schemaNode;

        var schemaJson = effectiveSchemaNode.ToJsonString();
        var cacheKey = ComputeSha256Hex(schemaJson);
        if (CompiledSchemaCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var details = BuildSchemaDetailsFromNode(effectiveSchemaNode);

        if (CompiledSchemaCache.Count > 10000) CompiledSchemaCache.Clear();
        CompiledSchemaCache[cacheKey] = details;

        return details;
    }

    private async Task<ResolvedSchemaDetails> CreateSchemaFromJsonAsync(string schemaJson, string? documentUri = null)
    {
        var resolvedSchemaJson = await _schemaResolverService.ResolveAsync(schemaJson, documentUri, auth: null);
        var effectiveSchemaJson = string.IsNullOrWhiteSpace(resolvedSchemaJson) ? schemaJson : resolvedSchemaJson;
        return BuildSchemaDetails(effectiveSchemaJson);
    }

    private static ResolvedSchemaDetails BuildSchemaDetails(string schemaJson)
    {
        var cacheKey = ComputeSha256Hex(schemaJson);
        if (CompiledSchemaCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var schemaNode = System.Text.Json.Nodes.JsonNode.Parse(schemaJson) ?? throw new InvalidOperationException("Schema JSON could not be parsed");
        var details = BuildSchemaDetailsFromNode(schemaNode);

        if (CompiledSchemaCache.Count > 10000) CompiledSchemaCache.Clear();
        CompiledSchemaCache[cacheKey] = details;

        return details;
    }

    private static ResolvedSchemaDetails BuildSchemaDetailsFromNode(System.Text.Json.Nodes.JsonNode schemaNode)
    {
        // Accept common OpenAPI-style schema keywords by normalizing them to JSON Schema.
        NormalizeSchemaNodeForDialect(schemaNode);

        var normalizedSchemaJson = schemaNode.ToJsonString();
        var builtSchema = JsonSchemaBuild.FromText(normalizedSchemaJson);
        var title = TryReadSchemaStringField(schemaNode, "title");
        var description = TryReadSchemaStringField(schemaNode, "description");
        return new ResolvedSchemaDetails(builtSchema, schemaNode, title, description);
    }

    private static void NormalizeSchemaNodeForDialect(System.Text.Json.Nodes.JsonNode node)
    {
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            if (obj.TryGetPropertyValue("example", out var exampleValue))
            {
                if (!obj.ContainsKey("examples"))
                {
                    var examples = new System.Text.Json.Nodes.JsonArray();
                    if (exampleValue != null)
                    {
                        examples.Add(exampleValue.DeepClone());
                    }

                    obj["examples"] = examples;
                }

                _ = obj.Remove("example");
            }

            _ = obj.Remove("name");

            var properties = obj.ToList();
            foreach (var (key, value) in properties)
            {
                if (value == null)
                {
                    continue;
                }

                if (IsSchemaMapKeyword(key) && value is System.Text.Json.Nodes.JsonObject schemaMap)
                {
                    foreach (var (_, mappedSchema) in schemaMap.ToList())
                    {
                        if (mappedSchema != null)
                        {
                            NormalizeSchemaNodeForDialect(mappedSchema);
                        }
                    }

                    continue;
                }

                if (IsSchemaArrayKeyword(key) && value is System.Text.Json.Nodes.JsonArray schemaArray)
                {
                    foreach (var item in schemaArray)
                    {
                        if (item != null)
                        {
                            NormalizeSchemaNodeForDialect(item);
                        }
                    }

                    continue;
                }

                if (IsSchemaObjectKeyword(key) && value is System.Text.Json.Nodes.JsonObject nestedSchema)
                {
                    NormalizeSchemaNodeForDialect(nestedSchema);
                }
            }

            return;
        }

        if (node is System.Text.Json.Nodes.JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    NormalizeSchemaNodeForDialect(item);
                }
            }
        }
    }

    private static bool IsSchemaMapKeyword(string keyword)
    {
        return keyword is "properties"
            or "patternProperties"
            or "$defs"
            or "definitions"
            or "dependentSchemas";
    }

    private static bool IsSchemaArrayKeyword(string keyword)
    {
        return keyword is "allOf"
            or "anyOf"
            or "oneOf"
            or "prefixItems";
    }

    private static bool IsSchemaObjectKeyword(string keyword)
    {
        return keyword is "items"
            or "contains"
            or "if"
            or "then"
            or "else"
            or "not"
            or "propertyNames"
            or "additionalProperties"
            or "unevaluatedItems"
            or "unevaluatedProperties"
            or "contentSchema";
    }

    private static string? TryReadSchemaStringField(System.Text.Json.Nodes.JsonNode? node, string fieldName)
    {
        if (node is not System.Text.Json.Nodes.JsonObject obj)
        {
            return null;
        }

        return obj[fieldName]?.GetValue<string>();
    }

    private static string ComputeSha256Hex(string value)
    {
        var maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(value.Length);
        var rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            var byteCount = System.Text.Encoding.UTF8.GetBytes(value, rentedBuffer);
            Span<byte> hashBytes = stackalloc byte[32]; // SHA256 hash is always 32 bytes
            System.Security.Cryptography.SHA256.HashData(rentedBuffer.AsSpan(0, byteCount), hashBytes);
            return Convert.ToHexString(hashBytes);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static int GetJsonDocumentUtf8Size(System.Text.Json.JsonDocument doc)
    {
        var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new System.Text.Json.Utf8JsonWriter(bufferWriter))
        {
            doc.RootElement.WriteTo(writer);
        }
        return bufferWriter.WrittenCount;
    }

    private static bool IsCycleOrDepthViolation(System.Text.Json.JsonException exception)
    {
        var message = exception.Message;
        return message.Contains("possible object cycle", StringComparison.OrdinalIgnoreCase)
            || message.Contains("maximum allowed depth", StringComparison.OrdinalIgnoreCase)
            || message.Contains("depth", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryFindCyclePath(object? root)
    {
        if (root == null || IsLeafValue(root.GetType()))
        {
            return null;
        }

        var stack = new Dictionary<object, string>(ReferenceEqualityComparer.Instance)
        {
            [root] = "$"
        };

        return TryFindCyclePathRecursive(root, "$", stack, depth: 0, visitedCount: 1);
    }

    private static string? TryFindCyclePathRecursive(object current, string currentPath, Dictionary<object, string> stack, int depth, int visitedCount)
    {
        const int maxTraversalDepth = 256;
        const int maxVisitedNodes = 100_000;

        if (depth >= maxTraversalDepth || visitedCount >= maxVisitedNodes)
        {
            return null;
        }

        foreach (var (segment, child) in EnumerateObjectChildren(current))
        {
            if (child == null)
            {
                continue;
            }

            var childType = child.GetType();
            if (IsLeafValue(childType))
            {
                continue;
            }

            var childPath = BuildChildPath(currentPath, segment, childType);

            if (stack.TryGetValue(child, out var seenPath))
            {
                return $"{childPath} (references {seenPath})";
            }

            stack[child] = childPath;
            var nested = TryFindCyclePathRecursive(child, childPath, stack, depth + 1, visitedCount + 1);
            if (nested != null)
            {
                return nested;
            }

            _ = stack.Remove(child);
        }

        return null;
    }

    private static IEnumerable<(string Segment, object? Value)> EnumerateObjectChildren(object value)
    {
        if (value is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? "?";
                yield return (key, entry.Value);
            }

            yield break;
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var i = 0;
            foreach (var item in enumerable)
            {
                yield return ($"[{i}]", item);
                i++;
            }

            yield break;
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            yield return (property.Name, propertyValue);
        }
    }

    private static string BuildChildPath(string parentPath, string segment, Type childType)
    {
        var isArrayIndex = segment.StartsWith('[');
        if (isArrayIndex)
        {
            return $"{parentPath}{segment}";
        }

        if (childType.IsGenericType && childType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            return $"{parentPath}.{segment}";
        }

        return $"{parentPath}.{segment}";
    }

    private static bool IsLeafValue(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        return type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(Uri);
    }

    private static ValidationError MapJsonStructureViolationToValidationError(JsonStructureViolationException exception)
    {
        var source = exception.SourceType switch
        {
            JsonStructureViolationSource.UserProvidedJson => "user provided JSON",
            JsonStructureViolationSource.CachedSchema => "cached schema",
            JsonStructureViolationSource.UserProvidedSchema => "user provided schema",
            _ => "JSON payload"
        };

        var code = exception.SourceType switch
        {
            JsonStructureViolationSource.UserProvidedJson => "JSON_STRUCTURE_VIOLATION",
            JsonStructureViolationSource.CachedSchema => "CACHED_SCHEMA_STRUCTURE_VIOLATION",
            JsonStructureViolationSource.UserProvidedSchema => "SCHEMA_STRUCTURE_VIOLATION",
            _ => "JSON_STRUCTURE_VIOLATION"
        };

        var line = ToNullableInt(exception.LineNumber);
        var column = ToNullableInt(exception.BytePositionInLine);
        var location = BuildLocationSuffix(line, column);
        var path = string.IsNullOrWhiteSpace(exception.JsonPath) ? "$" : exception.JsonPath!;
        var sourceIdentifier = string.IsNullOrWhiteSpace(exception.SourceIdentifier)
            ? "unknown"
            : exception.SourceIdentifier;

        var details = exception.ViolationKind switch
        {
            JsonStructureViolationKind.Cycle => "contains a circular reference",
            JsonStructureViolationKind.Depth => $"exceeds maximum depth of {MaxAllowedJsonDepth}",
            _ => $"contains circular references or exceeds maximum depth of {MaxAllowedJsonDepth}"
        };

        return new ValidationError
        {
            Path = path,
            Message = $"Validation failed: {source} {details}. Source: {sourceIdentifier}. JSON path: {path}{location}.",
            ErrorCode = code,
            Severity = "Error",
            LineNumber = line,
            ColumnNumber = column,
            SourceIdentifier = sourceIdentifier
        };
    }

    private static int? ToNullableInt(long? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value < 0)
        {
            return null;
        }

        if (value.Value > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)value.Value;
    }

    private static string BuildLocationSuffix(int? line, int? column)
    {
        if (!line.HasValue && !column.HasValue)
        {
            return string.Empty;
        }

        if (line.HasValue && column.HasValue)
        {
            return $", line {line.Value}, column {column.Value}";
        }

        if (line.HasValue)
        {
            return $", line {line.Value}";
        }

        return $", column {column!.Value}";
    }

    private static JsonStructureViolationKind GetViolationKind(System.Text.Json.JsonException exception)
    {
        var message = exception.Message;

        if (message.Contains("possible object cycle", StringComparison.OrdinalIgnoreCase))
        {
            return JsonStructureViolationKind.Cycle;
        }

        if (message.Contains("maximum allowed depth", StringComparison.OrdinalIgnoreCase)
            || message.Contains("depth", StringComparison.OrdinalIgnoreCase))
        {
            return JsonStructureViolationKind.Depth;
        }

        return JsonStructureViolationKind.Unknown;
    }

    private enum JsonStructureViolationSource
    {
        UserProvidedJson,
        CachedSchema,
        UserProvidedSchema
    }

    private enum JsonStructureViolationKind
    {
        Unknown,
        Cycle,
        Depth
    }

    private sealed class JsonStructureViolationException(JsonValidatorService.JsonStructureViolationSource sourceType, string sourceIdentifier, System.Text.Json.JsonException innerException, string? detectedPath = null) : Exception("JSON structure violates cycle/depth constraints.", innerException)
    {
        public JsonStructureViolationSource SourceType { get; } = sourceType;
        public string SourceIdentifier { get; } = sourceIdentifier;
        public string? JsonPath { get; } = !string.IsNullOrWhiteSpace(detectedPath) ? detectedPath : innerException.Path;
        public long? LineNumber { get; } = innerException.LineNumber;
        public long? BytePositionInLine { get; } = innerException.BytePositionInLine;
        public JsonStructureViolationKind ViolationKind { get; } = GetViolationKind(innerException);
    }

    private static Task<List<ValidationError>> ValidateJsonAgainstSchemaAsync(System.Text.Json.JsonDocument dataDocument, ResolvedSchemaDetails schema, ValidationOptions? options)
    {
        var errors = new List<ValidationError>();
        var maxErrors = options?.MaxErrors ?? 100;

        try
        {
            var evaluationOptions = new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                RequireFormatValidation = options?.ValidateFormat ?? true

            };

            var evaluation = schema.Schema.Evaluate(dataDocument.RootElement, evaluationOptions);
            if (evaluation.IsValid)
            {
                return Task.FromResult(errors);
            }

            errors.AddRange(FlattenEvaluationErrors(evaluation, dataDocument.RootElement).Take(maxErrors));
        }
        catch (System.Text.Json.JsonException ex)
        {
            errors.Add(new ValidationError
            {
                Path = "",
                Message = $"Invalid JSON format: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}",
                ErrorCode = "INVALID_JSON",
                Severity = "Error"
            });
        }
        catch (RefResolutionException ex)
        {
            errors.Add(new ValidationError
            {
                Path = "",
                Message = $"Schema reference could not be resolved: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}",
                ErrorCode = "SCHEMA_REFERENCE_UNRESOLVED",
                Severity = "Error"
            });
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                Path = "",
                Message = $"Validation failed: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}",
                ErrorCode = "VALIDATION_ERROR",
                Severity = "Error"
            });
        }

        return Task.FromResult(errors);
    }

    private static IEnumerable<ValidationError> FlattenEvaluationErrors(EvaluationResults root, System.Text.Json.JsonElement rootElement)
    {
        var stack = new Stack<EvaluationResults>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.Errors != null)
            {
                foreach (var error in current.Errors)
                {
                    var evaluationPath = current.EvaluationPath.ToString();
                    var isAdditionalProperty = error.Key.Contains("additionalProperties", StringComparison.OrdinalIgnoreCase)
                        || error.Value.Contains("additional properties", StringComparison.OrdinalIgnoreCase)
                        || evaluationPath.Contains("additionalProperties", StringComparison.OrdinalIgnoreCase);

                    var message = error.Value;
                    if (message.Contains("does not match format", StringComparison.OrdinalIgnoreCase))
                    {
                        var failedValue = current.InstanceLocation.Evaluate(rootElement);
                        if (failedValue.HasValue)
                        {
                            var valueStr = GetJsonElementRawOrStringValue(failedValue.Value);
                            message = $"{message} (failed value: \"{valueStr}\")";
                        }
                    }

                    yield return new ValidationError
                    {
                        Path = ConvertJsonPointerToPath(current.InstanceLocation.ToString()),
                        Message = message,
                        ErrorCode = isAdditionalProperty ? "ADDITIONAL_FIELD" : "VALIDATION_ERROR",
                        Severity = isAdditionalProperty ? "Info" : "Error"
                    };
                }
            }

            if (current.Details == null)
            {
                continue;
            }

            for (var i = current.Details.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Details[i]);
            }
        }
    }

    private static string GetJsonElementRawOrStringValue(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Number => element.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            System.Text.Json.JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    private static string ConvertJsonPointerToPath(string jsonPointer)
    {
        if (string.IsNullOrWhiteSpace(jsonPointer) || jsonPointer == "/")
        {
            return string.Empty;
        }

        var span = jsonPointer.AsSpan();
        if (span.StartsWith('/'))
        {
            span = span[1..];
        }

        var pathBuilder = new System.Text.StringBuilder(span.Length);

        while (!span.IsEmpty)
        {
            int slashIndex = span.IndexOf('/');
            var segment = slashIndex < 0 ? span : span[..slashIndex];

            if (segment.Length == 0)
            {
                if (slashIndex < 0)
                {
                    break;
                }
                span = span[(slashIndex + 1)..];
                continue;
            }

            if (int.TryParse(segment, out _))
            {
                pathBuilder.Append('[');
                AppendUnescaped(pathBuilder, segment);
                pathBuilder.Append(']');
            }
            else
            {
                if (pathBuilder.Length > 0)
                {
                    pathBuilder.Append('.');
                }
                AppendUnescaped(pathBuilder, segment);
            }

            if (slashIndex < 0)
            {
                break;
            }
            
            span = span[(slashIndex + 1)..];
        }

        return pathBuilder.ToString();
    }

    private static void AppendUnescaped(System.Text.StringBuilder builder, ReadOnlySpan<char> segment)
    {
        for (int i = 0; i < segment.Length; i++)
        {
            // Translate the RFC 6901 JSON Pointer escape characters manually
            if (segment[i] == '~' && i + 1 < segment.Length)
            {
                if (segment[i + 1] == '1')
                {
                    builder.Append('/');
                    i++;
                    continue;
                }
                if (segment[i + 1] == '0')
                {
                    builder.Append('~');
                    i++;
                    continue;
                }
            }
            builder.Append(segment[i]);
        }
    }

    private async Task<System.Text.Json.JsonDocument> FetchJsonDataFromUrlAsync(string dataUrl, ValidationOptions? options, CancellationToken cancellationToken)
    {
        return await _requestProcessingService.ExecuteWithRetryAsync(async (ct) =>
        {
            try
            {
                _logger.FetchingJsonDataFromUrl(dataUrl);
                var validatedUri = await _pathParsingService.ValidateAndParseDataUrlAsync(dataUrl, options);
                var httpClient = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, validatedUri);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                _ = response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.HttpRequestFailedFetchingData(ex, dataUrl);
                throw new InvalidOperationException($"Failed to fetch data from URL: {dataUrl}", ex);
            }
            catch (System.Text.Json.JsonException ex)
            {
                if (IsCycleOrDepthViolation(ex))
                {
                    _logger.UserJsonCycleOrDepthLimitExceeded(ex, MaxAllowedJsonDepth, dataUrl);
                    throw new JsonStructureViolationException(JsonStructureViolationSource.UserProvidedJson, dataUrl, ex);
                }

                _logger.InvalidJsonReceived(ex, dataUrl);
                throw new InvalidOperationException($"Invalid JSON received from URL: {dataUrl}", ex);
            }
        }, options, cancellationToken);
    }

    private string? GetSchemaTitle(ValidationRequest request, ResolvedSchemaDetails schema)
    {
        return TryGetSchemaStringFieldFromObject(request.Schema, "title") ?? schema.Title;
    }

    private string? GetSchemaDescription(ValidationRequest request, ResolvedSchemaDetails schema)
    {
        return TryGetSchemaStringFieldFromObject(request.Schema, "description") ?? schema.Description;
    }

    private string? GetSchemaTitleFromObject(object? schemaObject)
    {
        return TryGetSchemaStringFieldFromObject(schemaObject, "title");
    }

    private string? GetSchemaDescriptionFromObject(object? schemaObject)
    {
        return TryGetSchemaStringFieldFromObject(schemaObject, "description");
    }

    private string? TryGetSchemaStringFieldFromObject(object? schemaObject, string fieldName)
    {
        if (schemaObject == null)
        {
            return null;
        }

        try
        {
            var schemaJson = schemaObject switch
            {
                string schemaString => schemaString,
                System.Text.Json.Nodes.JsonNode schemaNode => schemaNode.ToJsonString(),
                System.Text.Json.JsonDocument schemaDocument => schemaDocument.RootElement.GetRawText(),
                System.Text.Json.JsonElement schemaElement => schemaElement.GetRawText(),
                _ => System.Text.Json.JsonSerializer.Serialize(schemaObject, DefaultSerializerOptions)
            };

            var schemaNodeText = System.Text.Json.Nodes.JsonNode.Parse(schemaJson);
            return TryReadSchemaStringField(schemaNodeText, fieldName);
        }
        catch (Exception ex)
        {
            if (fieldName == "title")
            {
                _logger.FailedToExtractTitleFromSchema(ex);
            }
            else if (fieldName == "description")
            {
                _logger.FailedToExtractDescriptionFromSchema(ex);
            }

            return null;
        }
    }

    /// <summary>
    /// Detects fields in the JSON data that are not defined in the schema.
    /// Returns a list of validation warnings for each additional field found.
    /// Normalizes array index segments (for example "items[0]" -> "items") and returns only unique results.
    /// </summary>
    private List<ValidationError> DetectAdditionalFields(System.Text.Json.JsonElement dataElement, System.Text.Json.Nodes.JsonNode? schemaNode)
    {
        var warnings = new List<ValidationError>();

        try
        {
            var pathSegments = new List<string>();
            DetectAdditionalFieldsRecursive(dataElement, schemaNode, pathSegments, warnings);

            // Normalize paths and keep only unique warnings by normalized path
            var uniqueWarnings = new Dictionary<string, ValidationError>();

            foreach (var warning in warnings)
            {
                if (!uniqueWarnings.ContainsKey(warning.Path))
                {
                    warning.Message = BuildAdditionalFieldMessage(warning.Path);
                    uniqueWarnings[warning.Path] = warning;
                }
            }

            return [.. uniqueWarnings.Values];
        }
        catch (Exception ex)
        {
            _logger.ErrorDetectingAdditionalFields(ex);
        }

        return warnings;
    }

    /// <summary>
    /// Recursively traverses the JSON data and schema to detect fields not defined in the schema.
    /// </summary>
    private void DetectAdditionalFieldsRecursive(System.Text.Json.JsonElement jsonElement, System.Text.Json.Nodes.JsonNode? schemaNode, List<string> pathSegments, List<ValidationError> warnings)
    {
        if (schemaNode == null)
        {
            return;
        }

        if (schemaNode is JsonObject obj && obj.TryGetPropertyValue("$ref", out var refNode) && refNode is JsonValue refValue)
        {
            var refStr = refValue.GetValue<string>();
            var resolvedNode = Task.Run(() => _schemaResolverService.ResolveNodeRefAsync(refStr)).GetAwaiter().GetResult();
            if (resolvedNode != null)
            {
                DetectAdditionalFieldsRecursive(jsonElement, resolvedNode, pathSegments, warnings);
            }
            return;
        }

        if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var schemaObject = schemaNode as System.Text.Json.Nodes.JsonObject;
            var additionalPropertiesNode = schemaObject?["additionalProperties"];

            foreach (var property in jsonElement.EnumerateObject())
            {
                pathSegments.Add(property.Name);
                var hasSchemaProperty = IsPropertyDefined(schemaNode, property.Name, out var propSchema);

                if (!hasSchemaProperty)
                {
                    warnings.Add(new ValidationError
                    {
                        Path = BuildPath(pathSegments),
                        ErrorCode = "ADDITIONAL_FIELD",
                        Severity = "Info"
                    });
                }

                if (hasSchemaProperty)
                {
                    DetectAdditionalFieldsRecursive(property.Value, propSchema, pathSegments, warnings);
                }
                else if (additionalPropertiesNode is System.Text.Json.Nodes.JsonObject additionalPropertiesSchema)
                {
                    DetectAdditionalFieldsRecursive(property.Value, additionalPropertiesSchema, pathSegments, warnings);
                }

                pathSegments.RemoveAt(pathSegments.Count - 1);
            }
        }
        else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var itemSchema = FindItemsSchema(schemaNode);

            if (itemSchema != null)
            {
                pathSegments.Add(ArrayIndexToken);
                foreach (var item in jsonElement.EnumerateArray())
                {
                    DetectAdditionalFieldsRecursive(item, itemSchema, pathSegments, warnings);
                }
                pathSegments.RemoveAt(pathSegments.Count - 1);
            }
        }
    }

    private bool IsPropertyDefined(System.Text.Json.Nodes.JsonNode? schemaNode, string propertyName, out System.Text.Json.Nodes.JsonNode? propertySchema)
    {
        propertySchema = null;
        if (schemaNode == null)
        {
            return false;
        }

        if (schemaNode is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("$ref", out var refNode) && refNode is JsonValue refValue)
            {
                var refStr = refValue.GetValue<string>();
                var resolvedNode = Task.Run(() => _schemaResolverService.ResolveNodeRefAsync(refStr)).GetAwaiter().GetResult();
                return IsPropertyDefined(resolvedNode, propertyName, out propertySchema);
            }

            if (obj.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject propsObj)
            {
                if (propsObj.TryGetPropertyValue(propertyName, out propertySchema))
                {
                    return true;
                }
            }

            if (obj.TryGetPropertyValue("allOf", out var allOfNode) && allOfNode is JsonArray allOfArr)
            {
                foreach (var item in allOfArr)
                {
                    if (IsPropertyDefined(item, propertyName, out propertySchema))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private System.Text.Json.Nodes.JsonNode? FindItemsSchema(System.Text.Json.Nodes.JsonNode? schemaNode)
    {
        if (schemaNode == null) return null;

        if (schemaNode is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("$ref", out var refNode) && refNode is JsonValue refValue)
            {
                var refStr = refValue.GetValue<string>();
                var resolvedNode = Task.Run(() => _schemaResolverService.ResolveNodeRefAsync(refStr)).GetAwaiter().GetResult();
                return FindItemsSchema(resolvedNode);
            }

            if (obj.TryGetPropertyValue("items", out var itemsSchema))
            {
                return itemsSchema;
            }

            if (obj.TryGetPropertyValue("allOf", out var allOfNode) && allOfNode is JsonArray allOfArr)
            {
                foreach (var item in allOfArr)
                {
                    var found = FindItemsSchema(item);
                    if (found != null) return found;
                }
            }
        }

        return null;
    }

    private static string BuildPath(List<string> segments)
    {
        if (segments.Count == 0) return string.Empty;
        var sb = new System.Text.StringBuilder(segments[0]);
        for (int i = 1; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (ReferenceEquals(segment, ArrayIndexToken))
            {
                sb.Append(segment);
            }
            else
            {
                sb.Append('.').Append(segment);
            }
        }
        return sb.ToString();
    }

    private static List<string> GetRequiredRootProperties(System.Text.Json.Nodes.JsonNode? schemaNode)
    {
        if (schemaNode is not System.Text.Json.Nodes.JsonObject schemaObject
            || schemaObject["required"] is not System.Text.Json.Nodes.JsonArray requiredArray)
        {
            return [];
        }

        return [.. requiredArray
            .Select(static item => item?.GetValue<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()];
    }

    private static bool HasRootTypeKeyword(System.Text.Json.Nodes.JsonNode? schemaNode)
    {
        return schemaNode is System.Text.Json.Nodes.JsonObject schemaObject
            && schemaObject.ContainsKey("type");
    }

    private static string BuildAdditionalFieldMessage(string path)
    {
        return $"Field '{path}' is not defined in the schema";
    }

    private sealed record ResolvedSchemaDetails(
        JsonSchema Schema,
        System.Text.Json.Nodes.JsonNode? SchemaNode,
        string? Title,
        string? Description);

}
