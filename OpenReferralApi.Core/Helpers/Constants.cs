using System.Text.RegularExpressions;

namespace OpenReferralApi.Core.Helpers;

internal static class Constants
{
    internal static readonly string[] OpenApiDocumentProbePaths =
  [
        "",
        "openapi.json",
        "openapi",
        "swagger.json",
        "swagger.yaml",
        "swagger.yml",
        ".well-known/openapi.json",
        "api-docs/openapi.json",
        "api-docs/openapi.yaml",
        "api-docs/openapi.yml",
        "api-docs",
        "v3/api-docs",
        "v2/api-docs",
        "swagger/v1/swagger.json",
        "swagger/v1/swagger.yaml",
        "swagger/v1/swagger.yml"
    ];

        internal static readonly string[] SwaggerConfigProbePaths =
    [
        "swagger-config",
        "swagger/swagger-config",
        "api-docs/swagger-config",
        "v3/api-docs/swagger-config",
        "v2/api-docs/swagger-config",
        "swagger/v1/swagger-config"
    ];

    internal static readonly string[] DocumentationUiProbePaths =
  [
        "swagger/index.html",
        "swagger",
        "api/swagger",
        "api/swagger/index.html",
        "scalar",
        "scalar/index.html",
        "swagger-ui",
        "swagger-ui/index.html",
        "swagger-ui.html",
        "redoc",
        "api/redoc",
        "docs",
        "api/docs"
    ];
    
    /// <summary>Detects OpenAPI/Swagger YAML roots by matching lines like: <c>openapi: 3.0.0</c> or <c>swagger: "2.0"</c>.</summary>
    internal static readonly Regex OpenApiYamlRegex = new(
        @"(?m)^\s*(openapi|swagger)\s*:\s*",
        RegexOptions.Compiled);

    /// <summary>Captures JSON/YAML definition URLs from Swagger UI config snippets like: <c>url: '/openapi.json'</c>.</summary>
    internal static readonly Regex SwaggerUiDefinitionUrlRegex = new(
        @"(?<![a-zA-Z0-9_])url\s*[:=]\s*[""']([^""']+\.(?:json|yaml|yml))[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Captures api-docs style endpoints from Swagger UI config snippets like: <c>url = '/v3/api-docs'</c>.</summary>
    internal static readonly Regex SwaggerUiApiDocsRegex = new(
        @"(?<![a-zA-Z0-9_])url\s*[:=]\s*[""']([^""']*api-docs[^""']*)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Captures Swagger UI config URL declarations such as: <c>configUrl: '/swagger-config'</c>.</summary>
    internal static readonly Regex SwaggerUiConfigUrlRegex = new(
        @"configUrl\s*[:=]\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Captures spec-url attribute values from HTML elements like: <c>spec-url="/openapi.json"</c>.</summary>
    internal static readonly Regex SpecUrlAttributeRegex = new(
        @"spec-url\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Captures the first argument passed to <c>Redoc.init(...)</c>, typically the spec URL.</summary>
    internal static readonly Regex RedocInitRegex = new(
        @"Redoc\.init\(\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.Singleline);

}