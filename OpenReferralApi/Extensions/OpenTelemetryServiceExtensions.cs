using OpenReferralApi.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenReferralApi.Extensions;

internal static class OpenTelemetryServiceExtensions
{
    public static void ConfigureOpenTelemetry(this WebApplicationBuilder builder)
    {
        var openTelemetryOptions = builder.Configuration
            .GetSection(OpenTelemetryOptions.SectionName)
            .Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();

        if (!openTelemetryOptions.Enabled)
        {
            return;
        }

        var otlpEndpoint = Uri.TryCreate(openTelemetryOptions.OtlpEndpoint, UriKind.Absolute, out var parsedOtlpEndpoint)
            ? parsedOtlpEndpoint
            : null;

        _ = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
                resourceBuilder.AddService(
                    serviceName: Instrumentation.ServiceName,
                    serviceVersion: Instrumentation.ServiceVersion))
            .WithTracing(tracingBuilder =>
            {
                _ = tracingBuilder
                    .AddSource(Instrumentation.ActivitySource.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (otlpEndpoint is not null)
                {
                    _ = tracingBuilder.AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = otlpEndpoint;
                    });
                }
                else if (builder.Environment.IsDevelopment())
                {
                    _ = tracingBuilder.AddConsoleExporter();
                }
            })
            .WithMetrics(metricsBuilder =>
            {
                _ = metricsBuilder
                    .AddMeter(Instrumentation.ServiceName)
                    .AddMeter("OpenReferralApi.Core.OpenApiValidationService")
                    .AddMeter("OpenReferralApi.Core.EndpointTestingService")
                    .AddMeter("OpenReferralApi.SchemaWarmupExecutor")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (otlpEndpoint is not null)
                {
                    _ = metricsBuilder.AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = otlpEndpoint;
                    });
                }
                else if (builder.Environment.IsDevelopment())
                {
                    _ = metricsBuilder.AddConsoleExporter();
                }
            });
    }
}
