using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenReferralApi.Core.Services;
using OpenReferralApi.Extensions;
using OpenReferralApi.HealthChecks;
using OpenReferralApi.Logging;
using OpenReferralApi.Middleware;
using OpenReferralApi.Services;
using OpenReferralApi.Swagger;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Just declare it here!
string[] databaseHealthTags = ["ready", "db"];

string[] selfHealthTags = ["ready"];

string[] serviceHealthTags = ["ready", "service"];

builder.Configuration.AddEnvironmentVariables("ORUK_API_");

// 1. Load standard Environment Variables (including those with your prefix)
builder.Configuration.AddEnvironmentVariables("ORUK_API_");

// 2. Load the Custom JSON Strings (These will override/patch the above)
// --- Specification URLs ---
var urlsJson = Environment.GetEnvironmentVariable("ORUK_API_Specification__UrlsJson");
if (!string.IsNullOrEmpty(urlsJson))
{
    var patch = $"{{\"Specification\":{{\"Urls\":{urlsJson}}}}}";
    using var urlsPatchStream = new MemoryStream(Encoding.UTF8.GetBytes(patch));
    builder.Configuration.AddJsonStream(urlsPatchStream);
}

// --- Schema URLs ---
var schemasJson = Environment.GetEnvironmentVariable("ORUK_API_SchemaResolution__KnownUrlsJson");
if (!string.IsNullOrEmpty(schemasJson))
{
    var patch = $"{{\"SchemaResolution\":{{\"KnownJsonSchemaUrls\":{schemasJson}}}}}";
    using var schemasPatchStream = new MemoryStream(Encoding.UTF8.GetBytes(patch));
    builder.Configuration.AddJsonStream(schemasPatchStream);
}

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Configure strongly-typed options
builder.Services.Configure<SpecificationOptions>(
    builder.Configuration.GetSection(SpecificationOptions.SectionName));

builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));

builder.Services.Configure<SchemaResolutionOptions>(
    builder.Configuration.GetSection(SchemaResolutionOptions.SectionName));

builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.Configure<FeedValidationOptions>(
    builder.Configuration.GetSection(FeedValidationOptions.SectionName));

builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection(SecurityOptions.SectionName));

builder.Services.Configure<RateLimitingOptions>(
    builder.Configuration.GetSection(RateLimitingOptions.SectionName));

builder.Services.Configure<OpenTelemetryOptions>(
    builder.Configuration.GetSection(OpenTelemetryOptions.SectionName));

builder.Services.Configure<OpenApiValidationServerOptions>(
    builder.Configuration.GetSection(OpenApiValidationServerOptions.SectionName));

// Add services to the container.
builder.Services.AddSwaggerDocumentation(builder.Configuration);

// CORS - Environment-specific origins
var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (securityOptions.AllowedCorsOrigins.Contains("*"))
        {
            _ = policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            _ = policy.WithOrigins(securityOptions.AllowedCorsOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Rate Limiting
var rateLimitingOptions = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    _ = options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = rateLimitingOptions.PermitLimit;
        opt.Window = TimeSpan.FromSeconds(rateLimitingOptions.Window);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = rateLimitingOptions.QueueLimit;
    });
});

// Configure HTTP client with environment-based security settings
builder.Services.AddHttpClient(nameof(OpenApiValidationService), client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "OpenReferral-Validator/1.0");
    client.Timeout = TimeSpan.FromMinutes(2);
})
.ConfigurePrimaryHttpMessageHandler(sp =>
{
    var securityOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecurityOptions>>().Value;
    var handler = new HttpClientHandler();

    if (!securityOpts.ValidateSslCertificates)
    {
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    }

    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

    return handler;
});

builder.Services.AddHttpClient();
builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager =>
    {
        manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Response Caching
builder.Services.AddResponseCaching();
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Cache());
    options.AddPolicy("MockEndpoints", builder =>
        builder.Expire(TimeSpan.FromMinutes(5)));
});

// Health Checks
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: selfHealthTags);

var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
if (!string.IsNullOrEmpty(databaseOptions.ConnectionString))
{
    // Register MongoDB client for health checks and feed validation
    _ = builder.Services.AddSingleton<MongoDB.Driver.IMongoClient>(sp =>
    {
        return new MongoDB.Driver.MongoClient(databaseOptions.ConnectionString);
    });

    _ = healthChecksBuilder.AddMongoDb(
        name: "mongodb",
        tags: databaseHealthTags);

    // Feed validation services - only register if MongoDB is configured
    _ = builder.Services.AddScoped<IFeedValidationService, FeedValidationService>();
    _ = builder.Services.AddHostedService<FeedValidationBackgroundService>();
}
else
{
    // Register null implementation when MongoDB is not configured
    _ = builder.Services.AddScoped<IFeedValidationService, NullFeedValidationService>();
}

healthChecksBuilder.AddCheck<FeedValidationHealthCheck>(
    "feed-validation",
    tags: serviceHealthTags);

// Services
builder.Services.AddScoped<IPathParsingService, PathParsingService>();
builder.Services.AddSingleton<IRequestProcessingService, RequestProcessingService>();
builder.Services.AddSingleton<ISchemaWarmupStatusTracker, SchemaWarmupStatusTracker>();
builder.Services.AddSingleton<ISchemaWarmupStatusProvider>(sp => sp.GetRequiredService<ISchemaWarmupStatusTracker>());

// Schema Resolver Service - resolves $ref in remote schema files for runtime schema validation
builder.Services.AddScoped<ISchemaResolverService, SchemaResolverService>();
builder.Services.AddHostedService<SchemaWarmupBackgroundService>();

builder.Services.AddScoped<IJsonValidatorService, JsonValidatorService>();
builder.Services.AddScoped<IAuthenticationValidationService, AuthenticationValidationService>();
builder.Services.AddScoped<IOpenApiSpecificationService, OpenApiSpecificationService>();
builder.Services.AddScoped<IHsdsComplianceService, HsdsComplianceService>();
builder.Services.AddScoped<IProfileResolverService, ProfileResolverService>();
builder.Services.AddScoped<IEndpointTestingService, EndpointTestingService>();
builder.Services.AddScoped<IOpenApiValidationService, OpenApiValidationService>();

builder.Services.AddScoped<IProfileDiscoveryService, ProfileDiscoveryService>();
builder.Services.AddScoped<IOpenReferralUKValidationResponseMapper, OpenReferralUKValidationResponseMapper>();

// Memory Cache configuration
builder.Services.AddMemoryCache(options =>
{
    var cacheOpts = builder.Configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
    options.SizeLimit = cacheOpts.MaxSizeMB * 1024 * 1024; // Convert MB to bytes
});

// Exception Handlers
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// OpenTelemetry Configuration
builder.ConfigureOpenTelemetry();

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

var openApiValidationSettings = app.Configuration
    .GetSection(OpenApiValidationServerOptions.SectionName)
    .Get<OpenApiValidationServerOptions>() ?? new OpenApiValidationServerOptions();

StartupLogger.LogSettings(app.Logger, openApiValidationSettings);

// Configure the HTTP request pipeline
app.UseExceptionHandler();

// Middleware
app.UseMiddleware<CorrelationIdMiddleware>();

// Enable Swagger in all environments
app.UseSwaggerDocumentation();

if (!app.Environment.IsDevelopment())
{
    _ = app.UseHsts();
}

// Health check endpoints
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
    Predicate = _ => false,
    ResponseWriter = async (context, _) =>
    {
        var warmupStatus = context.RequestServices.GetRequiredService<ISchemaWarmupStatusProvider>().GetSnapshot();

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            schemaWarmup = warmupStatus
        })).ConfigureAwait(false);
    }
});

app.UseRouting();
app.UseSerilogRequestLogging();
app.UseCors();
var configuredUrls = app.Configuration["ASPNETCORE_URLS"] ?? app.Configuration["urls"] ?? string.Empty;
var hasHttpsInUrls = configuredUrls
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
var hasExplicitHttpsPort = !string.IsNullOrWhiteSpace(app.Configuration["ASPNETCORE_HTTPS_PORT"]) ||
                           !string.IsNullOrWhiteSpace(app.Configuration["HTTPS_PORT"]);
var hasKestrelHttpsEndpoint = !string.IsNullOrWhiteSpace(app.Configuration["Kestrel:Endpoints:Https:Url"]);

if (hasHttpsInUrls || hasExplicitHttpsPort || hasKestrelHttpsEndpoint)
{
    _ = app.UseHttpsRedirection();
}
app.UseResponseCaching();
app.UseOutputCache();
app.UseRateLimiter();

app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");

app.Run();

// Ensure logs are flushed on shutdown
Log.CloseAndFlush();