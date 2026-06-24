using System.ComponentModel;
using System.Text.Json.Serialization;

namespace OpenReferralApi.Core.Models.Configuration;

public enum HsdsValidationMode
{
    Fast,
    Full
}

public enum OwnSchemaValidationMode
{
    None,
    AllowAdditionalProperties,
    Strict
}

/// <summary>
/// Configuration options for controlling OpenAPI validation and endpoint testing behavior
/// Allows fine-tuning of validation processes and testing parameters
/// </summary>
public class OpenApiValidationOptions : ValidationOptionsBase
{
    /// <summary>
    /// Whether to include response bodies in `OpenApiValidationResult` output.
    /// When true, `HttpTestResult.responseBody` will contain the actual response content.
    /// When false (default), response bodies are omitted to reduce payload size and avoid exposing sensitive data.
    /// Must be explicitly set to true to include response bodies in validation results.
    /// </summary>
    [DefaultValue(false)]
    [JsonPropertyName("includeResponseBody")]
    public bool IncludeResponseBody { get; set; } = false;

    /// <summary>
    /// Whether to include detailed test results array in the EndpointTestResult output.
    /// When true, the full `TestResults` collection with all HTTP request/response details will be included.
    /// When false (default), the TestResults array will be excluded to reduce payload size.
    /// Must be explicitly set to true to include detailed test results in validation output.
    /// Note: This only affects the TestResults collection; summary information and validation errors are always included.
    /// </summary>
    [JsonPropertyName("includeTestResults")]
    public bool IncludeTestResults { get; set; } = true;
}

/// <summary>
/// Server-side OpenAPI validation settings not overridable by client payloads.
/// </summary>
public class OpenApiValidationServerOptions
{
    public const string SectionName = "OpenApiValidation";

    /// <summary>
    /// Selects how the feed should be validated against its own discovered OpenAPI schema.
    /// None validates endpoint responses against the HSDS profile schema instead of the feed's schema.
    /// AllowAdditionalProperties validates against the feed's own schema but downgrades additional-field findings to warnings.
    /// Strict (default) validates against the feed's own schema and keeps additional-field findings as errors.
    /// </summary>
    [DefaultValue(OwnSchemaValidationMode.Strict)]
    public OwnSchemaValidationMode OwnSchemaValidation { get; set; } = OwnSchemaValidationMode.Strict;

    /// <summary>
    /// Selects the HSDS conformance depth.
    /// Fast performs strict feed-vs-own-spec runtime validation and feed-spec-vs-HSDS-spec comparison.
    /// Full additionally validates live feed responses against HSDS response schemas.
    /// </summary>
    [DefaultValue(HsdsValidationMode.Fast)]
    public HsdsValidationMode HsdsValidationMode { get; set; } = HsdsValidationMode.Fast;

    /// <summary>
    /// Whether to allow user-supplied authentication credentials for OpenAPI schema and data source requests.
    /// When enabled, authentication details provided in API requests will be used.
    /// When disabled, all requests are made without authentication.
    /// Default: false (for security)
    /// </summary>
    public bool AllowUserSuppliedAuth { get; set; } = false;

    /// <summary>
    /// Whether to validate the OpenAPI specification structure and compliance
    /// Includes schema validation, security analysis, and quality metrics
    /// Recommended to keep enabled for comprehensive validation
    /// </summary>
    [JsonPropertyName("validateSpecification")]
    public bool ValidateSpecification { get; set; } = true;

    /// <summary>
    /// Whether to perform live endpoint testing against the API server.
    /// Set to false for specification-only validation without HTTP requests.
    /// Requires a valid BaseUrl in the request when enabled.
    /// </summary>
    public bool TestEndpoints { get; set; } = true;

    /// <summary>
    /// Whether memory checkpoint logging/metrics are emitted during validation and endpoint testing.
    /// Disable to reduce memory instrumentation overhead and log volume.
    /// </summary>
    [DefaultValue(true)]
    public bool EnableMemoryCheckpointLogging { get; set; } = true;

    /// <summary>
    /// Whether to test optional endpoints that are marked as optional in the OpenAPI specification.
    /// When true, tests optional endpoints and accepts 404/501 responses as valid for unimplemented features.
    /// When false, skips endpoints tagged with "Optional".
    /// </summary>
    [DefaultValue(true)]
    public bool TestOptionalEndpoints { get; set; } = true;

    /// <summary>
    /// Whether to report non-implemented optional endpoints as warnings instead of errors.
    /// When true, optional endpoints returning 404/501 are logged as informational.
    /// When false, all endpoint failures are treated as errors regardless of optional status.
    /// </summary>
    [DefaultValue(true)]
    public bool TreatOptionalEndpointsAsWarnings { get; set; } = true;

    /// <summary>
    /// Maximum number of characters retained in each endpoint test response body when response bodies are included.
    /// Set to 0 or a negative value to disable truncation.
    /// This cap is applied server-side after validation/testing to bound response payload memory and output size.
    /// </summary>
    [DefaultValue(262144)]
    public int MaxRetainedResponseBodyCharacters { get; set; } = 262144;

    /// <summary>
    /// Maximum number of validation errors retained per endpoint response validation pass.
    /// Lower values reduce memory pressure for large payloads that generate many repeated errors.
    /// Set to 0 or a negative value to use the validator default.
    /// </summary>
    [DefaultValue(25)]
    public int MaxValidationErrorsPerResponse { get; set; } = 25;
}
