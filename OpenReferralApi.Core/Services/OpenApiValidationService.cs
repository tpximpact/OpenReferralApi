using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OpenReferralApi.Core.Models;

namespace OpenReferralApi.Core.Services;

public class OpenApiValidationService : IOpenApiValidationService
{
    private readonly ILogger<OpenApiValidationService> _logger;
    private readonly HttpClient _httpClient;

    public OpenApiValidationService(ILogger<OpenApiValidationService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<OpenApiValidationResult> ValidateOpenApiSpecificationAsync(OpenApiValidationRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new OpenApiValidationResult();
        result.OpenApiSchemaUrl = request.OpenApiSchemaUrl;

        try
        {
            _logger.LogInformation("Starting OpenAPI specification testing");

            // Get OpenAPI specification
            //JObject openApiSpec;
            // if (!string.IsNullOrEmpty(request.OpenApiSchemaUrl))
            // {
            //     var fetchedSchema = await FetchOpenApiSpecFromUrlAsync(request.OpenApiSchemaUrl, cancellationToken);
            //     openApiSpec = JObject.Parse(fetchedSchema.ToString());
            //     // External references are already resolved by FetchOpenApiSpecFromUrlAsync
            // }
            // else
            // {
            //     throw new ArgumentException("OpenApiSchemaUrl must be provided");
            // }

            // Validate the OpenAPI specification
            // OpenApiSpecificationValidation? specValidation = null;
            // if (request.Options.ValidateSpecification)
            // {
            //     specValidation = await ValidateOpenApiSpecificationInternalAsync(openApiSpec, cancellationToken);
            //     result.SpecificationValidation = specValidation;
            // }

            // Test endpoints if requested
            // List<EndpointTestResult> endpointTests = new();
            // if (request.Options.TestEndpoints && !string.IsNullOrEmpty(request.BaseUrl))
            // {
            //     endpointTests = await TestEndpointsAsync(openApiSpec, request.BaseUrl, request.Authentication, request.Options, request.OpenApiSchemaUrl, cancellationToken);
            //     result.EndpointTests = endpointTests;
            // }

            // Build summary
            // result.Summary = BuildTestSummary(specValidation, endpointTests);
            // result.IsValid = (specValidation?.IsValid ?? true) && result.Summary.FailedTests == 0;

            // Set metadata
            result.Metadata = new OpenApiValidationMetadata
            {
                BaseUrl = request.BaseUrl,
                TestTimestamp = DateTime.UtcNow,
                TestDuration = stopwatch.Elapsed,
                UserAgent = "OpenReferralApi Validation Service/1.0"
            };

            // _logger.LogInformation("OpenAPI testing completed. IsValid: {IsValid}, Endpoints: {EndpointCount}",
            //     result.IsValid, result.EndpointTests.Count);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OpenAPI testing");
            result.IsValid = false;
            // result.Summary = new OpenApiValidationSummary();
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }
}