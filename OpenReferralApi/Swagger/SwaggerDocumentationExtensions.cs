using System.Reflection;
using Microsoft.OpenApi;

namespace OpenReferralApi.Swagger;

/// <summary>
/// Swagger documentation extensions for OpenAPI 3.1 generation.
/// Note: The "Swagger" namespace is retained for backward compatibility and consistency with Swashbuckle.AspNetCore,
/// despite the API documentation now using OpenAPI 3.1 specification.
/// </summary>
internal static class SwaggerDocumentationExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.Configure<SwaggerDocumentationOptions>(
            configuration.GetSection(SwaggerDocumentationOptions.SectionName));

        var swaggerOptions = configuration
            .GetSection(SwaggerDocumentationOptions.SectionName)
            .Get<SwaggerDocumentationOptions>() ?? new SwaggerDocumentationOptions();

        _ = services.AddSingleton(new SwaggerRuntimeOptions(
            swaggerOptions.DocName,
            swaggerOptions.Version,
            swaggerOptions.Title,
            swaggerOptions.Description,
            swaggerOptions.ResolveSpecVersion()));

        _ = services.AddEndpointsApiExplorer();
        _ = services.AddSwaggerGen(options =>
        {
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            options.UseInlineDefinitionsForEnums();
            options.DescribeAllParametersInCamelCase();

            options.CustomOperationIds(apiDescription =>
            {
                var controller = apiDescription.ActionDescriptor.RouteValues["controller"];
                var action = apiDescription.ActionDescriptor.RouteValues["action"];
                var method = apiDescription.HttpMethod?.ToUpperInvariant();
                var relativePath = apiDescription.RelativePath
                    ?.Replace("/", "_", StringComparison.Ordinal)
                    ?.Replace("{", string.Empty, StringComparison.Ordinal)
                    .Replace("}", string.Empty, StringComparison.Ordinal);

                return $"{controller}_{action}_{method}_{relativePath}";
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Optional bearer token support for deployments that secure this API."
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-API-Key",
                In = ParameterLocation.Header,
                Description = "Optional API key support for deployments that secure this API."
            });

            options.SwaggerDoc(swaggerOptions.DocName, new()
            {
                Title = swaggerOptions.Title,
                Version = swaggerOptions.Version,
                Description = swaggerOptions.Description,
                Contact = new()
                {
                    Name = "Open Referral UK",
                    Url = new Uri("https://openreferraluk.org")
                }
            });
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        var runtimeOptions = app.Services.GetRequiredService<SwaggerRuntimeOptions>();

        _ = app.UseSwagger(options =>
        {
            options.OpenApiVersion = runtimeOptions.OpenApiVersion;
            options.PreSerializeFilters.Add((document, _) => SwaggerExamplesApplier.Apply(document));
        });

        _ = app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(
                $"/swagger/{runtimeOptions.DocName}/swagger.json",
                $"{runtimeOptions.Title} {runtimeOptions.Version}");
            options.RoutePrefix = string.Empty;
            options.DisplayRequestDuration();
        });

        return app;
    }

    private sealed record SwaggerRuntimeOptions(
        string DocName,
        string Version,
        string Title,
        string Description,
        OpenApiSpecVersion OpenApiVersion);
}
