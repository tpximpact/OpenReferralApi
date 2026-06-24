using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Helpers;
using ValidationError = OpenReferralApi.Core.Models.Validation.ValidationError;

namespace OpenReferralApi.Core.Services;

public interface IHsdsComplianceService
{
    string? ExtractClaimedProfileVersion(string? profileReason, string? schemaUrl);
    bool TryGetKnownHsdsSchemaUrl(string? profileVersion, out string schemaUrl);
    List<ValidationError> CompareFeedSpecAgainstHsdsProfile(JsonNode feedSpec, JsonNode hsdsSpec);
    Task ValidateEndpointResponsesAgainstHsdsProfileAsync(
        List<EndpointTestResult> endpointTests,
        JsonNode hsdsSpec,
        OpenApiValidationOptions options,
        CancellationToken cancellationToken);
    void ApplyAdditionalFieldPolicy(ValidationResult? validationResult, bool reportAdditionalFields);
}

public partial class HsdsComplianceService(
    IJsonValidatorService jsonValidatorService,
    IOptions<SpecificationOptions>? specificationOptions = null,
    IOptions<OpenApiValidationServerOptions>? openApiValidationOptions = null) : IHsdsComplianceService
{
    [GeneratedRegex(@"Standard version \[user:\s*(?<version>[^\]]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileReasonVersionRegex();

    [GeneratedRegex(@"/specifications/(?<version>[^/]+)/openapi\.json", RegexOptions.IgnoreCase)]
    private static partial Regex SchemaUrlVersionRegex();

    private static readonly HashSet<string> SupportedHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options", "trace"
    };

    // In-memory lookup table for known HSDS baseline schemas by profile version.
    private readonly IJsonValidatorService _jsonValidatorService = jsonValidatorService;
    private readonly Dictionary<string, string> _profileSchemaByVersion = BuildProfileSchemaLookup(specificationOptions?.Value);
    private readonly OpenApiValidationServerOptions? _openApiValidationOptions = openApiValidationOptions?.Value;

    public string? ExtractClaimedProfileVersion(string? profileReason, string? schemaUrl)
    {
        if (!string.IsNullOrWhiteSpace(profileReason))
        {
            var profileReasonMatch = ProfileReasonVersionRegex().Match(profileReason);
            if (profileReasonMatch.Success)
            {
                var extracted = profileReasonMatch.Groups["version"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return extracted;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(schemaUrl))
        {
            var urlMatch = SchemaUrlVersionRegex().Match(schemaUrl);
            if (urlMatch.Success)
            {
                var extracted = urlMatch.Groups["version"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return extracted;
                }
            }
        }

        return null;
    }

    public bool TryGetKnownHsdsSchemaUrl(string? profileVersion, out string schemaUrl)
    {
        schemaUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(profileVersion))
        {
            return false;
        }

        // Tier 1: exact match on the raw profile version string (case-insensitive)
        if (_profileSchemaByVersion.TryGetValue(profileVersion.Trim(), out var exactMatch))
        {
            schemaUrl = exactMatch;
            return true;
        }

        // Tier 2: numeric major.minor fallback — find the first configured key whose numeric part matches
        var requestedNumeric = ProfileVersionNormalizer.ExtractMajorMinor(profileVersion);
        if (!string.IsNullOrWhiteSpace(requestedNumeric))
        {
            foreach (var entry in _profileSchemaByVersion)
            {
                var keyNumeric = ProfileVersionNormalizer.ExtractMajorMinor(entry.Key);
                if (string.Equals(keyNumeric, requestedNumeric, StringComparison.OrdinalIgnoreCase))
                {
                    schemaUrl = entry.Value;
                    return true;
                }
            }
        }

        return false;
    }

    public List<ValidationError> CompareFeedSpecAgainstHsdsProfile(JsonNode feedSpec, JsonNode hsdsSpec)
    {
        var findings = new List<ValidationError>();

        var feedSpecObject = ToJsonObject(feedSpec);
        var hsdsSpecObject = ToJsonObject(hsdsSpec);
        if (feedSpecObject is null || hsdsSpecObject is null)
        {
            return findings;
        }

        var feedOperations = GetOperationMap(feedSpecObject, includeOptionalOperations: true);
        var hsdsAllOperations = GetOperationMap(hsdsSpecObject, includeOptionalOperations: true);
        var hsdsRequiredOperations = GetOperationMap(hsdsSpecObject, includeOptionalOperations: false);

        foreach (var requiredOperation in hsdsRequiredOperations.Keys)
        {
            if (!feedOperations.ContainsKey(requiredOperation))
            {
                findings.Add(new ValidationError
                {
                    Path = $"paths.{requiredOperation}",
                    Message = $"Missing required HSDS endpoint: {requiredOperation}",
                    ErrorCode = "HSDS_MISSING_ENDPOINT",
                    Severity = "Error"
                });
            }
        }

        foreach (var feedOperation in feedOperations.Keys)
        {
            if (!hsdsAllOperations.ContainsKey(feedOperation))
            {
                findings.Add(new ValidationError
                {
                    Path = $"paths.{feedOperation}",
                    Message = $"Additional endpoint not defined by HSDS profile: {feedOperation}",
                    ErrorCode = "HSDS_ADDITIONAL_ENDPOINT",
                    Severity = "Info"
                });
            }
        }

        var commonOperations = feedOperations.Keys.Intersect(hsdsRequiredOperations.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var operationKey in commonOperations)
        {
            var feedOperation = feedOperations[operationKey];
            var hsdsOperation = hsdsRequiredOperations[operationKey];

            var feedResponseSchema = GetPrimarySuccessResponseSchema(feedOperation);
            var hsdsResponseSchema = GetPrimarySuccessResponseSchema(hsdsOperation);
            CompareSchemaFields(
                findings,
                operationKey,
                scope: "response",
                feedSchema: feedResponseSchema,
                hsdsSchema: hsdsResponseSchema,
                missingFieldCode: "HSDS_MISSING_REQUIRED_FIELD",
                additionalFieldCode: "HSDS_ADDITIONAL_FIELD",
                missingFieldMessagePrefix: "Missing required HSDS field",
                additionalFieldMessagePrefix: "Additional field",
                missingSchemaCode: null,
                missingSchemaMessage: null);

            var feedRequestSchema = GetRequestBodySchema(feedOperation);
            var hsdsRequestSchema = GetRequestBodySchema(hsdsOperation);
            CompareSchemaFields(
                findings,
                operationKey,
                scope: "requestBody",
                feedSchema: feedRequestSchema,
                hsdsSchema: hsdsRequestSchema,
                missingFieldCode: "HSDS_MISSING_REQUIRED_REQUEST_FIELD",
                additionalFieldCode: "HSDS_ADDITIONAL_REQUEST_FIELD",
                missingFieldMessagePrefix: "Missing required HSDS request-body field",
                additionalFieldMessagePrefix: "Additional request-body field",
                missingSchemaCode: "HSDS_MISSING_REQUEST_BODY",
                missingSchemaMessage: "Missing request body schema required by HSDS profile");
        }

        return findings;
    }

    public async Task ValidateEndpointResponsesAgainstHsdsProfileAsync(
        List<EndpointTestResult> endpointTests,
        JsonNode hsdsSpec,
        OpenApiValidationOptions options,
        CancellationToken cancellationToken)
    {
        var hsdsSpecObject = ToJsonObject(hsdsSpec);
        if (hsdsSpecObject is null)
        {
            return;
        }

        var hsdsOperations = GetOperationMap(hsdsSpecObject, includeOptionalOperations: false);

        foreach (var endpoint in endpointTests)
        {
            var operationKey = $"{endpoint.Method?.ToUpperInvariant()} {endpoint.Path}";
            if (!hsdsOperations.TryGetValue(operationKey, out var hsdsOperation))
            {
                continue;
            }

            var hsdsResponseSchema = GetPrimarySuccessResponseSchema(hsdsOperation);
            if (hsdsResponseSchema == null)
            {
                continue;
            }

            var compiledHsdsSchema = BuildHsdsValidationSchema(hsdsResponseSchema, hsdsSpecObject);

            foreach (var testResult in endpoint.TestResults.Where(t => t.IsSuccessStatusCode && t.ResponseBody != null && t.ResponseBody.Length > 0))
            {
                var validationRequest = new ValidationRequest
                {
                    JsonData = testResult.ResponseBody,
                    Schema = compiledHsdsSchema,
                    Options = new ValidationOptions
                    {
                        MaxErrors = ResolveMaxValidationErrorsPerResponse(),
                        ReportAdditionalFields = true
                    }
                };

                var hsdsValidationResult = await _jsonValidatorService.ValidateAsync(validationRequest, cancellationToken);
                ApplyAdditionalFieldPolicy(hsdsValidationResult, options.ReportAdditionalFields);

                if (hsdsValidationResult.Errors.Count == 0)
                {
                    continue;
                }

                var mappedErrors = hsdsValidationResult.Errors.Select(error => new ValidationError
                {
                    Path = error.Path,
                    Message = $"HSDS runtime validation: {error.Message}",
                    ErrorCode = error.ErrorCode.Equals("ADDITIONAL_FIELD", StringComparison.OrdinalIgnoreCase)
                        ? "HSDS_RUNTIME_ADDITIONAL_FIELD"
                        : "HSDS_RUNTIME_VALIDATION_ERROR",
                    Severity = error.Severity,
                    LineNumber = error.LineNumber,
                    ColumnNumber = error.ColumnNumber
                }).ToList();

                if (testResult.ValidationResult == null)
                {
                    testResult.ValidationResult = new ValidationResult
                    {
                        IsValid = false,
                        Errors = mappedErrors,
                        SchemaVersion = hsdsValidationResult.SchemaVersion,
                        Duration = hsdsValidationResult.Duration
                    };
                }
                else
                {
                    testResult.ValidationResult.Errors.AddRange(mappedErrors);
                }

                if (mappedErrors.Any(e => string.Equals(e.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
                {
                    endpoint.Status = EndpointTestStatus.FailedValidation;
                }
                else if (endpoint.Status != EndpointTestStatus.FailedValidation)
                {
                    endpoint.Status = EndpointTestStatus.PassedWithWarnings;
                }
            }

            endpoint.RefreshFlattenedFields();
        }
    }

    public void ApplyAdditionalFieldPolicy(ValidationResult? validationResult, bool reportAdditionalFields)
    {
        if (validationResult?.Errors == null || validationResult.Errors.Count == 0)
        {
            return;
        }

        var additionalFieldErrors = validationResult.Errors
            .Where(e => string.Equals(e.ErrorCode, "ADDITIONAL_FIELD", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (additionalFieldErrors.Count == 0)
        {
            return;
        }

        var strictOwnSchemaValidation = (_openApiValidationOptions?.OwnSchemaValidation
            ?? OwnSchemaValidationMode.Strict) == OwnSchemaValidationMode.Strict;
        var additionalFieldSeverity = strictOwnSchemaValidation ? "Error" : "Warning";
        foreach (var error in additionalFieldErrors)
        {
            error.Severity = additionalFieldSeverity;
        }

        if (!reportAdditionalFields)
        {
            validationResult.Errors = [.. validationResult.Errors
                .Where(error =>
                    !string.Equals(error.ErrorCode, "ADDITIONAL_FIELD", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(error.Severity, "Error", StringComparison.OrdinalIgnoreCase))];
        }

        validationResult.IsValid = !validationResult.Errors.Any(e =>
            string.Equals(e.Severity, "Error", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> BuildProfileSchemaLookup(SpecificationOptions? options)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (options?.Urls != null)
        {
            MergeMappings(lookup, options.Urls);
        }

        return lookup;
    }

    private static void MergeMappings(Dictionary<string, string> destination, IReadOnlyDictionary<string, string>? source)
    {
        if (source == null)
        {
            return;
        }

        foreach (var pair in source)
        {
            var rawKey = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            var schemaUrl = pair.Value.Trim();
            if (!Uri.IsWellFormedUriString(schemaUrl, UriKind.Absolute))
            {
                continue;
            }

            destination[rawKey] = schemaUrl;
        }
    }

    private int ResolveMaxValidationErrorsPerResponse()
    {
        var configuredMaxErrors = _openApiValidationOptions?.MaxValidationErrorsPerResponse;
        return configuredMaxErrors.HasValue && configuredMaxErrors.Value > 0
            ? configuredMaxErrors.Value
            : 100;
    }

    private static Dictionary<string, JsonObject> GetOperationMap(JsonObject spec, bool includeOptionalOperations)
    {
        var operationMap = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (spec["paths"] is not JsonObject paths)
        {
            return operationMap;
        }

        foreach (var pathProperty in paths)
        {
            if (pathProperty.Value is not JsonObject pathItem)
            {
                continue;
            }

            foreach (var methodProperty in pathItem)
            {
                if (!SupportedHttpMethods.Contains(methodProperty.Key) || methodProperty.Value is not JsonObject operation)
                {
                    continue;
                }

                if (!includeOptionalOperations && operation.IsOptionalEndpoint())
                {
                    continue;
                }

                var operationKey = $"{methodProperty.Key.ToUpperInvariant()} {pathProperty.Key}";
                operationMap[operationKey] = operation;
            }
        }

        return operationMap;
    }

    private static JsonNode? GetPrimarySuccessResponseSchema(JsonObject operation)
    {
        if (operation["responses"] is not JsonObject responses)
        {
            return null;
        }

        var statusCodeKey = responses
            .Select(p => p.Key)
            .FirstOrDefault(name => name.StartsWith('2'));

        if (statusCodeKey == null)
        {
            return null;
        }

        if (responses[statusCodeKey] is not JsonObject responseObject ||
            responseObject["content"] is not JsonObject contentObject)
        {
            return null;
        }

        foreach (var contentEntry in contentObject)
        {
            if (contentEntry.Key.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return contentEntry.Value?["schema"];
            }
        }

        return null;
    }

    private static JsonSchema BuildHsdsValidationSchema(JsonNode hsdsResponseSchema, JsonObject hsdsSpecObject)
    {
        JsonNode schemaToCompile = hsdsResponseSchema.DeepClone();

        if (hsdsSpecObject["components"] is JsonObject components
            && hsdsResponseSchema is JsonObject schemaObject)
        {
            // Embed components so that refs like #/components/schemas/* are resolvable
            // when JsonSchema.Net evaluates the compiled schema.
            var schemaWithComponents = (JsonObject)schemaObject.DeepClone();
            if (!schemaWithComponents.ContainsKey("components"))
            {
                schemaWithComponents["components"] = components.DeepClone();
            }

            schemaToCompile = schemaWithComponents;
        }

        return JsonSchemaBuild.FromText(schemaToCompile.ToJsonString());
    }

    private static JsonNode? GetRequestBodySchema(JsonObject operation)
    {
        if (operation["requestBody"] is not JsonObject requestBodyObject ||
            requestBodyObject["content"] is not JsonObject contentObject)
        {
            return null;
        }

        foreach (var contentEntry in contentObject)
        {
            if (contentEntry.Key.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return contentEntry.Value?["schema"];
            }
        }

        return null;
    }

    private static JsonObject? ToJsonObject(JsonNode? token)
    {
        if (token is null)
        {
            return null;
        }

        return token as JsonObject;
    }

    private static void CompareSchemaFields(
        List<ValidationError> findings,
        string operationKey,
        string scope,
        JsonNode? feedSchema,
        JsonNode? hsdsSchema,
        string missingFieldCode,
        string additionalFieldCode,
        string missingFieldMessagePrefix,
        string additionalFieldMessagePrefix,
        string? missingSchemaCode,
        string? missingSchemaMessage)
    {
        if (hsdsSchema == null)
        {
            return;
        }

        if (feedSchema == null)
        {
            if (!string.IsNullOrWhiteSpace(missingSchemaCode) && !string.IsNullOrWhiteSpace(missingSchemaMessage))
            {
                findings.Add(new ValidationError
                {
                    Path = $"paths.{operationKey}.{scope}",
                    Message = $"{missingSchemaMessage} for endpoint {operationKey}",
                    ErrorCode = missingSchemaCode,
                    Severity = "Error"
                });
            }

            return;
        }

        var hsdsRequiredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractRequiredFieldPaths(hsdsSchema, string.Empty, hsdsRequiredFields);

        var feedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractAllFieldPaths(feedSchema, string.Empty, feedFields);

        foreach (var requiredField in hsdsRequiredFields)
        {
            if (!feedFields.Contains(requiredField))
            {
                findings.Add(new ValidationError
                {
                    Path = $"paths.{operationKey}.{scope}.{requiredField}",
                    Message = $"{missingFieldMessagePrefix} '{requiredField}' for endpoint {operationKey}",
                    ErrorCode = missingFieldCode,
                    Severity = "Error"
                });
            }
        }

        var hsdsFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractAllFieldPaths(hsdsSchema, string.Empty, hsdsFields);

        foreach (var feedField in feedFields)
        {
            if (!hsdsFields.Contains(feedField))
            {
                findings.Add(new ValidationError
                {
                    Path = $"paths.{operationKey}.{scope}.{feedField}",
                    Message = $"{additionalFieldMessagePrefix} '{feedField}' is not defined in HSDS profile for endpoint {operationKey}",
                    ErrorCode = additionalFieldCode,
                    Severity = "Info"
                });
            }
        }
    }

    private static void ExtractRequiredFieldPaths(JsonNode schemaToken, string prefix, ISet<string> result)
    {
        if (schemaToken is not JsonObject schemaObject)
        {
            return;
        }

        if (schemaObject["required"] is JsonArray requiredArray && schemaObject["properties"] is JsonObject properties)
        {
            foreach (var requiredToken in requiredArray)
            {
                var requiredName = requiredToken?.ToString();
                if (string.IsNullOrWhiteSpace(requiredName))
                {
                    continue;
                }

                var fullPath = string.IsNullOrEmpty(prefix) ? requiredName : $"{prefix}.{requiredName}";
                _ = result.Add(fullPath);

                if (properties[requiredName] is not null)
                {
                    ExtractRequiredFieldPaths(properties[requiredName]!, fullPath, result);
                }
            }
        }

        if (schemaObject["type"]?.ToString() == "array" && schemaObject["items"] != null)
        {
            var arrayPrefix = string.IsNullOrEmpty(prefix) ? "[]" : $"{prefix}[]";
            ExtractRequiredFieldPaths(schemaObject["items"]!, arrayPrefix, result);
        }

        if (schemaObject["allOf"] is JsonArray allOf)
        {
            foreach (var subSchema in allOf)
            {
                if (subSchema is not null)
                {
                    ExtractRequiredFieldPaths(subSchema, prefix, result);
                }
            }
        }
    }

    private static void ExtractAllFieldPaths(JsonNode schemaToken, string prefix, ISet<string> result)
    {
        if (schemaToken is not JsonObject schemaObject)
        {
            return;
        }

        if (schemaObject["properties"] is JsonObject properties)
        {
            foreach (var property in properties)
            {
                var fullPath = string.IsNullOrEmpty(prefix) ? property.Key : $"{prefix}.{property.Key}";
                _ = result.Add(fullPath);
                if (property.Value is not null)
                {
                    ExtractAllFieldPaths(property.Value, fullPath, result);
                }
            }
        }

        if (schemaObject["type"]?.ToString() == "array" && schemaObject["items"] != null)
        {
            var arrayPrefix = string.IsNullOrEmpty(prefix) ? "[]" : $"{prefix}[]";
            ExtractAllFieldPaths(schemaObject["items"]!, arrayPrefix, result);
        }

        if (schemaObject["allOf"] is JsonArray allOf)
        {
            foreach (var subSchema in allOf)
            {
                if (subSchema is not null)
                {
                    ExtractAllFieldPaths(subSchema, prefix, result);
                }
            }
        }
    }
}
