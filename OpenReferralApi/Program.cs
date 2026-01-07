using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OpenReferralApi.Core.Services;
using OpenReferralApi.Models.Settings;
using OpenReferralApi.Repositories;
using OpenReferralApi.Repositories.Interfaces;
using OpenReferralApi.Services;
using OpenReferralApi.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("ORUK_API_");

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// CORS (adjust as needed for your frontend domains)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure HTTP client with better compatibility settings
builder.Services.AddHttpClient(nameof(OpenApiValidationService), client =>
{
    // Set a standard user agent that most APIs accept
    client.DefaultRequestHeaders.Add("User-Agent", "OpenReferral-Validator/1.0");

    // Set reasonable timeout
    client.Timeout = TimeSpan.FromMinutes(2);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // Configure SSL/TLS settings for better compatibility
    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
    {
        // In production, you should validate certificates properly
        // For now, we'll be more permissive to handle various server configurations
        return true;
    };

    // Enable automatic decompression for better compatibility
    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

    return handler;
});

builder.Services.AddHttpClient();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Configuration
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<GithubSettings>(builder.Configuration.GetSection("Github"));
builder.Services.Configure<ValidatorSettings>(builder.Configuration.GetSection("Validator"));

// MongoDB Client
var mongoConnectionString = builder.Configuration.GetSection("Database:ConnectionString").Value;
if (!string.IsNullOrEmpty(mongoConnectionString))
{
    builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoConnectionString));
}

// Health Checks
builder.Services.AddHealthChecks()
    .AddMongoDb(
        name: "mongodb",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "mongodb", "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "ready" })
    .AddCheck<PeriodicValidationService>("background-service", 
        failureStatus: HealthStatus.Degraded, 
        tags: new[] { "services", "background" });

// Repository
builder.Services.AddSingleton<IDataRepository, DataRepository>();

// Services
builder.Services.AddScoped<IPaginationTestingService, PaginationTestingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IValidatorService, ValidatorService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IGithubService, GithubService>();
builder.Services.AddScoped<ITestProfileService, TestProfileService>();
builder.Services.AddScoped<IPathParsingService, PathParsingService>();
builder.Services.AddSingleton<IRequestProcessingService, RequestProcessingService>();
builder.Services.AddScoped<IJsonSchemaResolverService, JsonSchemaResolverService>();
builder.Services.AddScoped<IJsonValidatorService, JsonValidatorService>();
builder.Services.AddScoped<IOpenApiValidationService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<OpenApiValidationService>>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(OpenApiValidationService));
    var jsonValidatorService = provider.GetRequiredService<IJsonValidatorService>();
    var schemaResolverService = provider.GetRequiredService<IJsonSchemaResolverService>();
    return new OpenApiValidationService(logger, httpClient, jsonValidatorService, schemaResolverService);
});

// Register OpenAPI discovery service used by controller to discover openapi_url from BaseUrl
builder.Services.AddScoped<IOpenApiDiscoveryService, OpenApiDiscoveryService>();

// Register OpenAPI to ValidationResponse mapper
builder.Services.AddScoped<IOpenApiToValidationResponseMapper, OpenApiToValidationResponseMapper>();

// Background service
builder.Services.AddSingleton<PeriodicValidationService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PeriodicValidationService>());

builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "JSON Validator API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

// Health check endpoints with detailed responses
app.MapHealthChecks("/health-check", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health-check/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health-check/live", new HealthCheckOptions
{
    Predicate = _ => false, // Just returns 200 OK if app is running
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new 
        { 
            status = "Healthy",
            timestamp = DateTime.UtcNow
        }));
    }
});

app.UseRouting();
app.UseCors();
app.UseHttpsRedirection();

app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");

app.Run();
