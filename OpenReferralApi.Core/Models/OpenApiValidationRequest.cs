using Newtonsoft.Json;

namespace OpenReferralApi.Core.Models;

/// <summary>
/// Request model for initiating OpenAPI specification validation and testing
/// Contains all necessary information to validate specs and optionally test live endpoints
/// </summary>
public class OpenApiValidationRequest
{
        /// <summary>
    /// URL to fetch the OpenAPI specification from (JSON or YAML)
    /// The service will download and parse the specification from this URL
    /// Supports HTTP/HTTPS URLs and handles $ref resolution for external references
    /// </summary>
    [JsonProperty("openApiSchemaUrl")]
    public string? OpenApiSchemaUrl { get; set; }
    
    /// <summary>
    /// Base URL of the live API server for endpoint testing
    /// Required if endpoint testing is enabled in options
    /// Should include protocol (http/https) and may include port (e.g., "https://api.example.com:8080")
    /// </summary>
    [JsonProperty("baseUrl")]
    public string? BaseUrl { get; set; }

}
