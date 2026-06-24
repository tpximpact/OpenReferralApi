using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;
using ValidationError = OpenReferralApi.Core.Models.Validation.ValidationError;

namespace OpenReferralApi.Core.Services;

public interface IOpenApiSpecificationService
{
    Task<OpenApiSpecificationValidation> ValidateAsync(JsonObject openApiSpec, CancellationToken cancellationToken = default);
}

public partial class OpenApiSpecificationService(
    ILogger<OpenApiSpecificationService> logger,
    IJsonValidatorService jsonValidatorService,
    IOptions<SchemaResolutionOptions>? schemaResolutionOptions = null) : IOpenApiSpecificationService
{
    private readonly ILogger<OpenApiSpecificationService> _logger = logger;
    private readonly IJsonValidatorService _jsonValidatorService = jsonValidatorService;
    private readonly IOptions<SchemaResolutionOptions> _schemaResolutionOptions = schemaResolutionOptions ?? Options.Create(new SchemaResolutionOptions());

    [GeneratedRegex(@"\$ref")]
    private static partial Regex RefRegex();

    public async Task<OpenApiSpecificationValidation> ValidateAsync(JsonObject openApiSpec, CancellationToken cancellationToken = default)
    {
        var validation = new OpenApiSpecificationValidation();
        var errors = new List<ValidationError>();

        try
        {
            _logger.ValidatingOpenApiSpecification();

            await ValidateOpenApiSpecObjectAsync(openApiSpec, validation, errors, cancellationToken);

            validation.SchemaAnalysis = AnalyzeSchemaStructure(openApiSpec);
            validation.QualityMetrics = AnalyzeQualityMetrics(openApiSpec);
            validation.Recommendations = GenerateRecommendations(openApiSpec, errors);

            return validation;
        }
        catch (Exception ex)
        {
            _logger.ErrorDuringOpenApiValidation(ex);
            errors.Add(new ValidationError
            {
                Path = "",
                Message = $"Validation error: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}",
                ErrorCode = "VALIDATION_ERROR",
                Severity = "Error"
            });

            validation.IsValid = false;
            validation.Errors = errors;
            return validation;
        }
    }

    private async Task ValidateOpenApiSpecObjectAsync(
        JsonObject specObject,
        OpenApiSpecificationValidation validation,
        List<ValidationError> errors,
        CancellationToken cancellationToken = default)
    {
        if (!specObject.ContainsKey("openapi") && !specObject.ContainsKey("swagger"))
        {
            errors.Add(new ValidationError
            {
                Path = "",
                Message = "OpenAPI specification must contain 'openapi' or 'swagger' field",
                ErrorCode = "MISSING_OPENAPI_VERSION",
                Severity = "Error"
            });
        }

        if (specObject.ContainsKey("openapi"))
        {
            validation.OpenApiVersion = specObject["openapi"]?.ToString();
        }
        else if (specObject.ContainsKey("swagger"))
        {
            validation.OpenApiVersion = specObject["swagger"]?.ToString();
        }

        if (!specObject.ContainsKey("info"))
        {
            errors.Add(new ValidationError
            {
                Path = "info",
                Message = "OpenAPI specification must contain 'info' section",
                ErrorCode = "MISSING_INFO",
                Severity = "Error"
            });
        }
        else
        {
            var info = specObject["info"];
            validation.Title = info?["title"]?.ToString();
            validation.Version = info?["version"]?.ToString();

            if (string.IsNullOrEmpty(validation.Title))
            {
                errors.Add(new ValidationError
                {
                    Path = "info.title",
                    Message = "API title is recommended",
                    ErrorCode = "MISSING_TITLE",
                    Severity = "Warning"
                });
            }

            if (string.IsNullOrEmpty(validation.Version))
            {
                errors.Add(new ValidationError
                {
                    Path = "info.version",
                    Message = "API version is recommended",
                    ErrorCode = "MISSING_VERSION",
                    Severity = "Warning"
                });
            }
        }

        if (!specObject.ContainsKey("paths"))
        {
            errors.Add(new ValidationError
            {
                Path = "paths",
                Message = "OpenAPI specification must contain 'paths' section",
                ErrorCode = "MISSING_PATHS",
                Severity = "Error"
            });
        }
        else
        {
            var paths = specObject["paths"];
            if (paths is JsonObject pathsObject)
            {
                validation.EndpointCount = pathsObject.Count;

                if (validation.EndpointCount == 0)
                {
                    errors.Add(new ValidationError
                    {
                        Path = "paths",
                        Message = "No endpoints defined in paths section",
                        ErrorCode = "NO_ENDPOINTS",
                        Severity = "Warning"
                    });
                }
            }
        }

        try
        {
            var schemaUri = this.GetOpenApiSchemaUri(specObject, validation.OpenApiVersion);
            if (!string.IsNullOrEmpty(schemaUri))
            {
                var validationRequest = new ValidationRequest
                {
                    JsonData = specObject,
                    SchemaUri = schemaUri
                };

                var schemaValidation = await _jsonValidatorService.ValidateAsync(validationRequest, cancellationToken);
                if (schemaValidation.Errors.Count > 0)
                {
                    errors.AddRange(schemaValidation.Errors);
                }

                var dialectInfo = specObject.ContainsKey("jsonSchemaDialect")
                    ? $"using jsonSchemaDialect: {TextSanitizer.SanitizeStringForLogging(specObject["jsonSchemaDialect"]?.ToString() ?? string.Empty)}"
                    : $"using version-based schema for OpenAPI {validation.OpenApiVersion}";
                _logger.ValidatedOpenApiSpecification(dialectInfo, schemaUri);
            }
            else
            {
                var dialectInfo = specObject.ContainsKey("jsonSchemaDialect")
                    ? $"jsonSchemaDialect '{specObject["jsonSchemaDialect"]}' is not supported"
                    : $"version '{validation.OpenApiVersion}' is not supported";

                errors.Add(new ValidationError
                {
                    Path = "",
                    Message = $"No schema validation available: {dialectInfo}. Supported versions: OpenAPI 3.0.x, 3.1.x, Swagger 2.0, and common JSON Schema dialects (2020-12, 2019-09, draft-07, draft-06, draft-04)",
                    ErrorCode = "UNSUPPORTED_SCHEMA_VERSION",
                    Severity = "Error"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.CouldNotValidateAgainstSchema(ex);
            errors.Add(new ValidationError
            {
                Path = "",
                Message = $"Could not validate against OpenAPI schema: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}",
                ErrorCode = "SCHEMA_VALIDATION_FAILED",
                Severity = "Error"
            });
        }

        validation.Errors = ValidationErrorNormalizer.NormalizeAndDeduplicateByPath(errors);
        validation.IsValid = !validation.Errors.Any(e => string.Equals(e.Severity, "Error", StringComparison.OrdinalIgnoreCase));

        _logger.OpenApiValidationCompleted(validation.IsValid, validation.Errors.Count);
    }

    private string? GetOpenApiSchemaUri(JsonObject specObject, string? version)
    {
        if (specObject.ContainsKey("jsonSchemaDialect"))
        {
            var dialect = specObject["jsonSchemaDialect"]?.ToString();
            if (!string.IsNullOrEmpty(dialect))
            {
                if (this.IsKnownJsonSchemaDialect(dialect))
                {
                    return dialect;
                }

                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(version))
        {
            // Use the root schema from configured KnownJsonSchemaUrls
            var knownUrls = _schemaResolutionOptions.Value.KnownJsonSchemaUrls;
            if (knownUrls?.Count > 0)
            {
                return knownUrls[0];
            }
        }

        return null;
    }

    private bool IsKnownJsonSchemaDialect(string dialect)
    {
        var knownUrls = _schemaResolutionOptions.Value.KnownJsonSchemaUrls;
        return knownUrls?.Contains(dialect, StringComparer.OrdinalIgnoreCase) == true;
    }

    private SchemaAnalysis AnalyzeSchemaStructure(JsonObject specObject)
    {
        var analysis = new SchemaAnalysis();

        try
        {
            if (specObject.ContainsKey("components"))
            {
                var components = specObject["components"];
                if (components is JsonObject componentsObject)
                {
                    analysis.ComponentCount = 1;

                    if (componentsObject.ContainsKey("schemas"))
                    {
                        var schemas = componentsObject["schemas"];
                        if (schemas is JsonObject schemasObject)
                        {
                            analysis.SchemaCount = schemasObject.Count;
                        }
                    }

                    if (componentsObject.ContainsKey("responses"))
                    {
                        var responses = componentsObject["responses"];
                        if (responses is JsonObject responsesObject)
                        {
                            analysis.ResponseCount = responsesObject.Count;
                        }
                    }

                    if (componentsObject.ContainsKey("parameters"))
                    {
                        var parameters = componentsObject["parameters"];
                        if (parameters is JsonObject parametersObject)
                        {
                            analysis.ParameterCount = parametersObject.Count;
                        }
                    }

                    if (componentsObject.ContainsKey("requestBodies"))
                    {
                        var requestBodies = componentsObject["requestBodies"];
                        if (requestBodies is JsonObject requestBodiesObject)
                        {
                            analysis.RequestBodyCount = requestBodiesObject.Count;
                        }
                    }

                    if (componentsObject.ContainsKey("headers"))
                    {
                        var headers = componentsObject["headers"];
                        if (headers is JsonObject headersObject)
                        {
                            analysis.HeaderCount = headersObject.Count;
                        }
                    }

                    if (componentsObject.ContainsKey("links"))
                    {
                        var links = componentsObject["links"];
                        if (links is JsonObject linksObject)
                        {
                            analysis.LinkCount = linksObject.Count;
                        }
                    }

                    if (componentsObject.ContainsKey("callbacks"))
                    {
                        var callbacks = componentsObject["callbacks"];
                        if (callbacks is JsonObject callbacksObject)
                        {
                            analysis.CallbackCount = callbacksObject.Count;
                        }
                    }
                }
            }

            if (specObject.ContainsKey("definitions"))
            {
                var definitions = specObject["definitions"];
                if (definitions is JsonObject definitionsObject)
                {
                    analysis.SchemaCount = definitionsObject.Count;
                }
            }

            analysis.ExampleCount = CountExamplesInSpec(specObject);

            var specJson = specObject.ToString();
            var refMatches = RefRegex().Matches(specJson);
            analysis.ReferencesResolved = refMatches.Count;
        }
        catch (Exception ex)
        {
            _logger.ErrorAnalyzingSchemaStructure(ex);
        }

        return analysis;
    }

    private int CountExamplesInSpec(JsonObject specObject)
    {
        int exampleCount = 0;

        try
        {
            if (specObject.ContainsKey("components"))
            {
                var components = specObject["components"];
                if (components is JsonObject componentsObject && componentsObject.ContainsKey("examples"))
                {
                    var examples = componentsObject["examples"];
                    if (examples is JsonObject examplesObject)
                    {
                        exampleCount += examplesObject.Count;
                    }
                }
            }

            if (specObject.ContainsKey("paths"))
            {
                var paths = specObject["paths"];
                if (paths is JsonObject pathsObject)
                {
                    foreach (var path in pathsObject)
                    {
                        if (path.Value is JsonObject pathObject)
                        {
                            foreach (var operation in pathObject)
                            {
                                if (operation.Value is JsonObject operationObject)
                                {
                                    if (operationObject.ContainsKey("requestBody"))
                                    {
                                        var requestBody = operationObject["requestBody"];
                                        if (requestBody is JsonObject requestBodyObject && requestBodyObject.ContainsKey("content"))
                                        {
                                            var content = requestBodyObject["content"];
                                            if (content is JsonObject contentObject)
                                            {
                                                foreach (var mediaType in contentObject)
                                                {
                                                    if (mediaType.Value is JsonObject mediaTypeObject)
                                                    {
                                                        if (mediaTypeObject.ContainsKey("example"))
                                                        {
                                                            exampleCount++;
                                                        }
                                                        if (mediaTypeObject.ContainsKey("examples"))
                                                        {
                                                            var examples = mediaTypeObject["examples"];
                                                            if (examples is JsonObject examplesObject)
                                                            {
                                                                exampleCount += examplesObject.Count;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (operationObject.ContainsKey("responses"))
                                    {
                                        var responses = operationObject["responses"];
                                        if (responses is JsonObject responsesObject)
                                        {
                                            foreach (var response in responsesObject)
                                            {
                                                if (response.Value is JsonObject responseObject && responseObject.ContainsKey("content"))
                                                {
                                                    var content = responseObject["content"];
                                                    if (content is JsonObject contentObject)
                                                    {
                                                        foreach (var mediaType in contentObject)
                                                        {
                                                            if (mediaType.Value is JsonObject mediaTypeObject)
                                                            {
                                                                if (mediaTypeObject.ContainsKey("example"))
                                                                {
                                                                    exampleCount++;
                                                                }
                                                                if (mediaTypeObject.ContainsKey("examples"))
                                                                {
                                                                    var examples = mediaTypeObject["examples"];
                                                                    if (examples is JsonObject examplesObject)
                                                                    {
                                                                        exampleCount += examplesObject.Count;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorCountingExamples(ex);
        }

        return exampleCount;
    }

    private QualityMetrics AnalyzeQualityMetrics(JsonObject specObject)
    {
        var metrics = new QualityMetrics();

        try
        {
            if (specObject.ContainsKey("paths"))
            {
                var paths = specObject["paths"];
                if (paths is JsonObject pathsObject)
                {
                    int totalEndpoints = 0;
                    int endpointsWithDescription = 0;
                    int endpointsWithSummary = 0;
                    int endpointsWithExamples = 0;
                    int totalParameters = 0;
                    int parametersWithDescription = 0;
                    int totalResponseCodes = 0;
                    int responseCodesDocumented = 0;

                    foreach (var path in pathsObject)
                    {
                        if (path.Value is JsonObject pathObject)
                        {
                            foreach (var method in pathObject)
                            {
                                if (method.Value is JsonObject operationObject)
                                {
                                    totalEndpoints++;

                                    if (operationObject.ContainsKey("description") &&
                                        !string.IsNullOrWhiteSpace(operationObject["description"]?.ToString()))
                                    {
                                        endpointsWithDescription++;
                                    }

                                    if (operationObject.ContainsKey("summary") &&
                                        !string.IsNullOrWhiteSpace(operationObject["summary"]?.ToString()))
                                    {
                                        endpointsWithSummary++;
                                    }

                                    if (HasExamples(operationObject))
                                    {
                                        endpointsWithExamples++;
                                    }

                                    if (operationObject.ContainsKey("parameters"))
                                    {
                                        var parameters = operationObject["parameters"];
                                        if (parameters is JsonArray parametersArray)
                                        {
                                            totalParameters += parametersArray.Count;
                                            parametersWithDescription += parametersArray
                                                .Where(p => p is JsonObject pObj &&
                                                       pObj.ContainsKey("description") &&
                                                       !string.IsNullOrWhiteSpace(pObj["description"]?.ToString()))
                                                .Count();
                                        }
                                    }

                                    if (operationObject.ContainsKey("responses"))
                                    {
                                        var responses = operationObject["responses"];
                                        if (responses is JsonObject responsesObject)
                                        {
                                            totalResponseCodes += responsesObject.Count;
                                            responseCodesDocumented += responsesObject
                                                .Where(r => r.Value is JsonObject rObj &&
                                                       rObj.ContainsKey("description") &&
                                                       !string.IsNullOrWhiteSpace(rObj["description"]?.ToString()))
                                                .Count();
                                        }
                                    }
                                }
                            }
                        }
                    }

                    metrics.EndpointsWithDescription = endpointsWithDescription;
                    metrics.EndpointsWithSummary = endpointsWithSummary;
                    metrics.EndpointsWithExamples = endpointsWithExamples;
                    metrics.ParametersWithDescription = parametersWithDescription;
                    metrics.TotalParameters = totalParameters;
                    metrics.ResponseCodesDocumented = responseCodesDocumented;
                    metrics.TotalResponseCodes = totalResponseCodes;

                    if (totalEndpoints > 0)
                    {
                        metrics.DocumentationCoverage = (double)endpointsWithDescription / totalEndpoints * 100;
                    }
                }
            }

            CountSchemaDescriptions(specObject, metrics);
            CalculateQualityScore(metrics);
        }
        catch (Exception ex)
        {
            _logger.ErrorAnalyzingQualityMetrics(ex);
        }

        return metrics;
    }

    private static bool HasExamples(JsonObject operationObject)
    {
        if (operationObject.ContainsKey("requestBody"))
        {
            var requestBody = operationObject["requestBody"];
            if (requestBody is JsonObject requestBodyObject && HasContentExamples(requestBodyObject))
            {
                return true;
            }
        }

        if (operationObject.ContainsKey("responses"))
        {
            var responses = operationObject["responses"];
            if (responses is JsonObject responsesObject)
            {
                foreach (var response in responsesObject)
                {
                    if (response.Value is JsonObject responseObject && HasContentExamples(responseObject))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasContentExamples(JsonObject contentContainer)
    {
        if (contentContainer.ContainsKey("content"))
        {
            var content = contentContainer["content"];
            if (content is JsonObject contentObject)
            {
                foreach (var mediaType in contentObject)
                {
                    if (mediaType.Value is JsonObject mediaTypeObject)
                    {
                        if (mediaTypeObject.ContainsKey("example") || mediaTypeObject.ContainsKey("examples"))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private static void CountSchemaDescriptions(JsonObject specObject, QualityMetrics metrics)
    {
        if (specObject.ContainsKey("components"))
        {
            var components = specObject["components"];
            if (components is JsonObject componentsObject && componentsObject.ContainsKey("schemas"))
            {
                var schemas = componentsObject["schemas"];
                if (schemas is JsonObject schemasObject)
                {
                    metrics.TotalSchemas = schemasObject.Count;
                    metrics.SchemasWithDescription = schemasObject
                        .Where(s => s.Value is JsonObject sObj &&
                               sObj.ContainsKey("description") &&
                               !string.IsNullOrWhiteSpace(sObj["description"]?.ToString()))
                        .Count();
                }
            }
        }

        if (specObject.ContainsKey("definitions"))
        {
            var definitions = specObject["definitions"];
            if (definitions is JsonObject definitionsObject)
            {
                metrics.TotalSchemas = definitionsObject.Count;
                metrics.SchemasWithDescription = definitionsObject
                    .Where(d => d.Value is JsonObject dObj &&
                           dObj.ContainsKey("description") &&
                           !string.IsNullOrWhiteSpace(dObj["description"]?.ToString()))
                    .Count();
            }
        }
    }

    private static void CalculateQualityScore(QualityMetrics metrics)
    {
        double score = 0;
        int factors = 0;

        if (metrics.DocumentationCoverage > 0)
        {
            score += metrics.DocumentationCoverage * 0.3;
            factors++;
        }

        if (metrics.TotalParameters > 0)
        {
            double parameterScore = (double)metrics.ParametersWithDescription / metrics.TotalParameters * 100;
            score += parameterScore * 0.25;
            factors++;
        }

        if (metrics.TotalSchemas > 0)
        {
            double schemaScore = (double)metrics.SchemasWithDescription / metrics.TotalSchemas * 100;
            score += schemaScore * 0.25;
            factors++;
        }

        if (metrics.TotalResponseCodes > 0)
        {
            double responseScore = (double)metrics.ResponseCodesDocumented / metrics.TotalResponseCodes * 100;
            score += responseScore * 0.20;
            factors++;
        }

        metrics.QualityScore = factors > 0 ? score / factors : 0;
    }

    private List<Recommendation> GenerateRecommendations(JsonObject specObject, List<ValidationError> errors)
    {
        var recommendations = new List<Recommendation>();

        try
        {
            foreach (var error in errors.Where(e => e.Severity == "Error"))
            {
                recommendations.Add(new Recommendation
                {
                    Type = "Error",
                    Category = "Validation",
                    Priority = "High",
                    Message = error.Message,
                    Path = error.Path,
                    ActionRequired = "Fix this validation error to ensure spec compliance",
                    Impact = "API consumers may not be able to use the specification correctly"
                });
            }

            foreach (var error in errors.Where(e => e.Severity == "Warning"))
            {
                recommendations.Add(new Recommendation
                {
                    Type = "Warning",
                    Category = "Best Practice",
                    Priority = "Medium",
                    Message = error.Message,
                    Path = error.Path,
                    ActionRequired = "Consider addressing this warning to improve spec quality",
                    Impact = "May affect usability or developer experience"
                });
            }

            AddQualityRecommendations(specObject, recommendations);
        }
        catch (Exception ex)
        {
            _logger.ErrorGeneratingRecommendations(ex);
        }

        return recommendations;
    }

    private static void AddQualityRecommendations(JsonObject specObject, List<Recommendation> recommendations)
    {
        if (!specObject.ContainsKey("info") || specObject["info"] is not JsonObject infoObject)
        {
            return;
        }

        if (!infoObject.ContainsKey("description") || string.IsNullOrWhiteSpace(infoObject["description"]?.ToString()))
        {
            recommendations.Add(new Recommendation
            {
                Type = "Improvement",
                Category = "Documentation",
                Priority = "Medium",
                Message = "API description is missing or empty",
                Path = "info.description",
                ActionRequired = "Add a comprehensive description of your API's purpose and functionality",
                Impact = "Helps developers understand the API's capabilities and use cases"
            });
        }

        if (!infoObject.ContainsKey("contact"))
        {
            recommendations.Add(new Recommendation
            {
                Type = "Improvement",
                Category = "Documentation",
                Priority = "Low",
                Message = "Contact information is missing",
                Path = "info.contact",
                ActionRequired = "Add contact information for API support",
                Impact = "Helps users get support when needed"
            });
        }

        if (!infoObject.ContainsKey("license"))
        {
            recommendations.Add(new Recommendation
            {
                Type = "Improvement",
                Category = "Legal",
                Priority = "Low",
                Message = "License information is missing",
                Path = "info.license",
                ActionRequired = "Add license information for your API",
                Impact = "Clarifies usage rights and restrictions"
            });
        }

        AddEndpointQualityRecommendations(specObject, recommendations);
    }

    private static void AddEndpointQualityRecommendations(JsonObject specObject, List<Recommendation> recommendations)
    {
        if (!HasServerMetadata(specObject))
        {
            recommendations.Add(new Recommendation
            {
                Type = "Improvement",
                Category = "Documentation",
                Priority = "Medium",
                Message = "Server/base URL metadata is missing",
                Path = "servers",
                ActionRequired = "Add 'servers' (OpenAPI 3.x) or host/basePath/schemes (Swagger 2.0) so client tooling can resolve endpoint URLs consistently",
                Impact = "Improves endpoint testing reliability and machine-readability for client generators"
            });
        }

        if (!specObject.ContainsKey("paths") || specObject["paths"] is not JsonObject pathsObject)
        {
            return;
        }

        foreach (var path in pathsObject)
        {
            if (path.Value is not JsonObject pathObject)
            {
                continue;
            }

            foreach (var method in pathObject)
            {
                if (!IsOperationMethod(method.Key) || method.Value is not JsonObject operationObject)
                {
                    continue;
                }

                var operationPath = $"paths.{path.Key}.{method.Key}";

                if (string.IsNullOrWhiteSpace(operationObject["operationId"]?.ToString()))
                {
                    recommendations.Add(new Recommendation
                    {
                        Type = "Improvement",
                        Category = "Best Practice",
                        Priority = "Medium",
                        Message = "Operation ID is missing",
                        Path = $"{operationPath}.operationId",
                        ActionRequired = "Add a stable, unique operationId for this endpoint",
                        Impact = "Improves machine-readable integrations, SDK generation, and traceability in validation reports"
                    });
                }

                if (!TryGetResponsesObject(operationObject, out var responsesObject))
                {
                    recommendations.Add(new Recommendation
                    {
                        Type = "Improvement",
                        Category = "Testing",
                        Priority = "High",
                        Message = "Responses object is missing for this operation",
                        Path = $"{operationPath}.responses",
                        ActionRequired = "Define response status codes and payload structures for this operation",
                        Impact = "Improves endpoint validation quality and ensures consumers can handle expected outcomes"
                    });

                    continue;
                }

                var hasErrorResponse = responsesObject.Any(p => IsErrorStatusCode(p.Key));
                if (!hasErrorResponse)
                {
                    recommendations.Add(new Recommendation
                    {
                        Type = "Improvement",
                        Category = "Testing",
                        Priority = "High",
                        Message = "No error response codes are documented",
                        Path = $"{operationPath}.responses",
                        ActionRequired = "Document at least one 4xx and/or 5xx response for this operation",
                        Impact = "Improves feed validation accuracy by allowing negative-path behavior to be tested consistently"
                    });
                }

                var successResponseWithoutSchema = responsesObject
                    .Where(p => IsSuccessStatusCode(p.Key) && p.Value is JsonObject)
                    .Any(p => ResponseHasSchema(p.Value as JsonObject) == false);

                if (successResponseWithoutSchema)
                {
                    recommendations.Add(new Recommendation
                    {
                        Type = "Improvement",
                        Category = "Data Quality",
                        Priority = "High",
                        Message = "One or more success responses are missing a response schema",
                        Path = $"{operationPath}.responses",
                        ActionRequired = "Add explicit schemas for 2xx responses (response.content.*.schema in OpenAPI 3.x or response.schema in Swagger 2.0)",
                        Impact = "Enables stronger runtime payload validation and improves feed quality checks"
                    });
                }
            }
        }
    }

    private static bool HasServerMetadata(JsonObject specObject)
    {
        if (specObject["servers"] is JsonArray servers && servers.Count > 0)
        {
            return true;
        }

        var host = specObject["host"]?.ToString();
        var basePath = specObject["basePath"]?.ToString();
        var schemes = specObject["schemes"] as JsonArray;

        return !string.IsNullOrWhiteSpace(host)
               || !string.IsNullOrWhiteSpace(basePath)
               || (schemes != null && schemes.Count > 0);
    }

    private static bool IsOperationMethod(string methodName)
    {
        return methodName.Equals("get", StringComparison.OrdinalIgnoreCase)
               || methodName.Equals("post", StringComparison.OrdinalIgnoreCase)
               || methodName.Equals("put", StringComparison.OrdinalIgnoreCase)
               || methodName.Equals("patch", StringComparison.OrdinalIgnoreCase)
               || methodName.Equals("delete", StringComparison.OrdinalIgnoreCase)
               || methodName.Equals("head", StringComparison.OrdinalIgnoreCase)
               || methodName.Equals("options", StringComparison.OrdinalIgnoreCase)
               || methodName.Equals("trace", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetResponsesObject(JsonObject operationObject, out JsonObject responsesObject)
    {
        responsesObject = null!;
        if (operationObject["responses"] is not JsonObject responses)
        {
            return false;
        }

        responsesObject = responses;
        return true;
    }

    private static bool IsSuccessStatusCode(string responseCode)
    {
        return responseCode.Length == 3
               && responseCode[0] == '2'
               && responseCode.All(char.IsDigit);
    }

    private static bool IsErrorStatusCode(string responseCode)
    {
        if (responseCode.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return responseCode.Length == 3
               && (responseCode[0] == '4' || responseCode[0] == '5')
               && responseCode.All(char.IsDigit);
    }

    private static bool ResponseHasSchema(JsonObject? responseObject)
    {
        if (responseObject == null)
        {
            return false;
        }

        // OpenAPI 3.x: responses.<code>.content.<mediaType>.schema
        if (responseObject["content"] is JsonObject contentObject)
        {
            foreach (var mediaType in contentObject)
            {
                if (mediaType.Value is JsonObject mediaTypeObject && mediaTypeObject["schema"] != null)
                {
                    return true;
                }
            }
        }

        // Swagger 2.0: responses.<code>.schema
        return responseObject["schema"] != null;
    }

}
