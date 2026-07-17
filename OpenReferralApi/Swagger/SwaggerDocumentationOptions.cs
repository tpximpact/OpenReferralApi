using Microsoft.OpenApi;

namespace OpenReferralApi.Swagger;

/// <summary>
/// Configuration options for Swagger/OpenAPI documentation.
/// Note: The "Swagger" naming is retained for consistency with Swashbuckle.AspNetCore NuGet package.
/// The API currently generates OpenAPI 3.1 specification.
/// </summary>
internal sealed class SwaggerDocumentationOptions
{
    public const string SectionName = "Swagger";

    public string DocName { get; set; } = "v3";

    public string Version { get; set; } = "3.1";

    public string Title { get; set; } = "Open Referral UK API";

    public string Description { get; set; } = "API for validating and monitoring Open Referral UK data feeds";

    [ConfigurationKeyName("OpenApiSpecVersion")]
    public string OpenApiSpecVersionName { get; set; } = "OpenApi3_1_0";

    public OpenApiSpecVersion ResolveSpecVersion()
    {
        return Enum.TryParse<OpenApiSpecVersion>(OpenApiSpecVersionName, ignoreCase: true, out var parsed)
            ? parsed
            : OpenApiSpecVersion.OpenApi2_0;
    }
}
