using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;
using ValidationError = OpenReferralApi.Core.Models.Validation.ValidationError;

namespace OpenReferralApi.Core.Services;

public interface IEndpointTestingService
{
    Task<List<EndpointTestResult>> TestEndpointsAsync(
    JsonObject openApiSpec,
        string baseUrl,
        OpenApiValidationOptions options,
        DataSourceAuthentication? authentication,
        CancellationToken cancellationToken = default);
}

public partial class EndpointTestingService(
    ILogger<EndpointTestingService> logger,
    IHttpClientFactory httpClientFactory,
    IJsonValidatorService jsonValidatorService,
    IHsdsComplianceService hsdsComplianceService,
    IOptions<OpenApiValidationServerOptions>? openApiValidationOptions = null) : OpenApiValidationServiceBase, IEndpointTestingService
{
    private const string EndpointTestingMetricsMeterName = "OpenReferralApi.Core.EndpointTestingService";
    private static readonly Meter EndpointTestingMetricsMeter = new(EndpointTestingMetricsMeterName, "1.0.0");
    private static readonly Histogram<long> EndpointTestingManagedHeapBytesHistogram = EndpointTestingMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.endpoint_testing.memory.managed_heap_bytes",
        unit: "By",
        description: "Managed heap size observed at endpoint testing memory checkpoints");
    private static readonly Histogram<long> EndpointTestingManagedHeapDeltaBytesHistogram = EndpointTestingMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.endpoint_testing.memory.managed_heap_delta_bytes",
        unit: "By",
        description: "Managed heap delta between endpoint testing memory checkpoints");
    private static readonly Histogram<long> EndpointTestingWorkingSetBytesHistogram = EndpointTestingMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.endpoint_testing.memory.working_set_bytes",
        unit: "By",
        description: "Process working set observed at endpoint testing memory checkpoints");
    private static readonly Histogram<long> EndpointTestingWorkingSetDeltaBytesHistogram = EndpointTestingMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.endpoint_testing.memory.working_set_delta_bytes",
        unit: "By",
        description: "Process working set delta between endpoint testing memory checkpoints");
    private readonly ILogger<EndpointTestingService> _logger = logger;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IJsonValidatorService _jsonValidatorService = jsonValidatorService;
    private readonly IHsdsComplianceService _hsdsComplianceService = hsdsComplianceService;
    private readonly OpenApiValidationServerOptions? _openApiValidationOptions = openApiValidationOptions?.Value;
    private readonly ConcurrentDictionary<string, JsonSchema> _validationSchemaCache = new(StringComparer.Ordinal);

    private static readonly string[][] TotalPagesPaths =
    [
        ["total_pages"],
        ["totalPages"],
        ["pagination", "total_pages"],
        ["pagination", "totalPages"],
        ["meta", "total_pages"],
        ["meta", "totalPages"]
    ];
    private static readonly string[] CollectionPropertyNames = ["data", "items", "results", "content", "contents"];
    private static readonly string[] ItemCountPropertyNames = ["size", "count", "length"];
    private static readonly string[] FallbackIdNames = ["id", "Id", "ID", "uuid", "guid"];
    private static readonly string[] SchemaCombiners = ["allOf", "anyOf", "oneOf"];
    private static readonly HashSet<string> ValidHttpMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE" };
    public async Task<List<EndpointTestResult>> TestEndpointsAsync(JsonObject openApiSpec, string baseUrl, OpenApiValidationOptions options, DataSourceAuthentication? authentication, CancellationToken cancellationToken = default)
    {
        return await TestEndpointsInternalAsync(openApiSpec, baseUrl, options, authentication, cancellationToken);
    }

    private async Task<List<EndpointTestResult>> TestEndpointsInternalAsync(JsonObject openApiSpec, string baseUrl, OpenApiValidationOptions options, DataSourceAuthentication? authentication, CancellationToken cancellationToken = default)
    {
        var results = new List<EndpointTestResult>();
        var parsedResponseJsonByResult = new ConcurrentDictionary<HttpTestResult, JsonDocument>();
        var extractedIds = new ConcurrentDictionary<string, List<string>>();
        var stopwatch = Stopwatch.StartNew();
        var memoryCheckpointTracker = new MemoryCheckpointTracker(stopwatch);

        void LogMemoryCheckpoint(string stage, string groupName)
        {
            if (!(_openApiValidationOptions?.EnableMemoryCheckpointLogging ?? true))
            {
                return;
            }

            var snapshot = memoryCheckpointTracker.Capture();
            const int compiledSchemaCacheEntryCount = 0;
            const long compiledSchemaCacheTotalKeyChars = 0;
            var (parsedJsonDocumentsInFlight,
                 retainedResponseBodies,
                 retainedResponseBodyChars,
                 extractedIdRoots,
                 extractedIdValues,
                 validationSchemaCacheEntries) = GetEndpointRetentionSnapshot(
                    results,
                    parsedResponseJsonByResult,
                    extractedIds,
                    _validationSchemaCache);

            var payload = CreateMemoryCheckpointPayload(
                service: nameof(EndpointTestingService),
                stage: stage,
                sanitizedBaseUrl: TextSanitizer.SanitizeUrlForLogging(baseUrl),
                snapshot: snapshot,
                groupName: TextSanitizer.SanitizeForLogging(groupName),
                accumulatedEndpointResults: results.Count) with
            {
                CompiledSchemaCacheEntryCount = compiledSchemaCacheEntryCount,
                CompiledSchemaCacheTotalKeyChars = compiledSchemaCacheTotalKeyChars,
                ParsedJsonDocumentsInFlight = parsedJsonDocumentsInFlight,
                RetainedResponseBodies = retainedResponseBodies,
                RetainedResponseBodyChars = retainedResponseBodyChars,
                ExtractedIdRoots = extractedIdRoots,
                ExtractedIdValues = extractedIdValues,
                ValidationSchemaCacheEntries = validationSchemaCacheEntries
            };

            _logger.UnifiedMemoryCheckpoint(payload);

            var tags = new TagList
            {
                { "stage", stage }
            };
            EndpointTestingManagedHeapBytesHistogram.Record(snapshot.ManagedHeapBytes, tags);
            EndpointTestingManagedHeapDeltaBytesHistogram.Record(snapshot.ManagedHeapDeltaBytes, tags);
            EndpointTestingWorkingSetBytesHistogram.Record(snapshot.ProcessWorkingSetBytes, tags);
            EndpointTestingWorkingSetDeltaBytesHistogram.Record(snapshot.ProcessWorkingSetDeltaBytes, tags);

            _logger.CompiledEndpointSchemaCacheState(
                stage,
                compiledSchemaCacheEntryCount,
                compiledSchemaCacheTotalKeyChars);

            _logger.EndpointTestingRetentionSnapshot(
                stage,
                parsedJsonDocumentsInFlight,
                retainedResponseBodies,
                retainedResponseBodyChars,
                extractedIdRoots,
                extractedIdValues,
                validationSchemaCacheEntries);

        }

        try
        {
            _logger.TestingEndpointsWithDependencyOrdering();
            LogMemoryCheckpoint("start", "all");

            // Guard on paths at the JsonObject boundary first.
            if (!openApiSpec.ContainsKey("paths"))
            {
                _logger.NoPathsFound();
                return results;
            }

            if (openApiSpec["paths"] is not JsonObject pathsObject)
            {
                return results;
            }

            // Group and order endpoints with intelligent dependency handling
            var endpointGroups = GroupEndpointsByDependencies(pathsObject);

            _logger.FoundEndpointGroups(endpointGroups.Count);
            LogMemoryCheckpoint("grouping-complete", "all");

            // Test endpoints in dependency order - collection endpoints first, then parameterized
            foreach (var group in endpointGroups)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.TestingEndpointGroup(TextSanitizer.SanitizeForLogging(group.RootPath), group.Endpoints.Count);
                }
                LogMemoryCheckpoint("group-start", group.RootPath);

                var semaphore = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);

                // PHASE 1: Test collection endpoints sequentially to extract IDs
                // These endpoints (e.g., GET /users) return collections with IDs that are stored in extractedIds
                foreach (var endpoint in group.CollectionEndpoints)
                {
                    var result = await TestSingleEndpointWithIdExtractionAsync(endpoint.Path, endpoint.Method, endpoint.Operation,
                        baseUrl, options, authentication, extractedIds, semaphore, openApiSpec, endpoint.PathItem, parsedResponseJsonByResult, cancellationToken);
                    results.Add(result);
                }

                LogMemoryCheckpoint("group-collections-complete", group.RootPath);

                // PHASE 2: Test parameterized endpoints concurrently using extracted IDs
                // These endpoints (e.g., GET /users/{id}) use IDs from the extractedIds dictionary
                var parameterizedTasks = new List<Task<EndpointTestResult>>();
                foreach (var endpoint in group.ParameterizedEndpoints)
                {
                    var task = TestSingleEndpointWithIdSubstitutionAsync(endpoint.Path, endpoint.Method, endpoint.Operation,
                        baseUrl, options, authentication, extractedIds, semaphore, openApiSpec, endpoint.PathItem, parsedResponseJsonByResult, cancellationToken);
                    parameterizedTasks.Add(task);
                }

                var parameterizedResults = await Task.WhenAll(parameterizedTasks);
                results.AddRange(parameterizedResults);

                semaphore.Dispose();

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.CompletedEndpointGroup(TextSanitizer.SanitizeForLogging(group.RootPath), group.CollectionEndpoints.Count, group.ParameterizedEndpoints.Count);
                }
                LogMemoryCheckpoint("group-complete", group.RootPath);
            }

            _logger.CompletedTestingEndpoints(results.Count);
            LogMemoryCheckpoint("complete", "all");
        }
        catch (Exception ex)
        {
            _logger.ErrorDuringEndpointTesting(ex);
        }

        return results;
    }

    private async Task<EndpointTestResult> TestSingleEndpointAsync(string path, string method, JsonObject operation, string baseUrl, OpenApiValidationOptions options, DataSourceAuthentication? authentication, SemaphoreSlim semaphore, JsonObject openApiDocument, JsonObject pathItem, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult, CancellationToken cancellationToken, string? testedId = null, bool retainResponseJson = false)
    {
        await semaphore.WaitAsync(cancellationToken);

        // Resolve all parameter references upfront (includes path-level and operation-level params)
        var resolvedParams = ResolveOperationParameters(operation, pathItem);

        var result = new EndpointTestResult
        {
            Path = path,
            Method = method,
            Name = operation["name"]?.ToString(),
            OperationId = operation["operationId"]?.ToString(),
            Summary = operation["summary"]?.ToString(),
            IsOptional = operation.IsOptionalEndpoint(),
            Status = EndpointTestStatus.NotTested
        };

        try
        {
            bool isOptional = operation.IsOptionalEndpoint();
            bool skipOptional = !(_openApiValidationOptions?.TestOptionalEndpoints ?? true) && isOptional;
            if (skipOptional)
            {
                result.Status = EndpointTestStatus.Skipped;
                return result;
            }

            // Check if this endpoint has pagination support
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.CheckingPaginationSupport(TextSanitizer.SanitizeStringForLogging(method), TextSanitizer.SanitizeStringForLogging(path));
            }
            bool hasPagination = method == "GET" && HasPageParameter(resolvedParams);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.PaginationCheckResult(TextSanitizer.SanitizeStringForLogging(method), TextSanitizer.SanitizeStringForLogging(path), hasPagination);
            }

            if (hasPagination)
            {
                // Test pagination: first page, middle page(s), last page
                await TestPaginatedEndpointAsync(result, path, method, operation, baseUrl, options, authentication, resolvedParams, openApiDocument, pathItem, parsedResponseJsonByResult, cancellationToken, retainFirstPageResponseJson: retainResponseJson);
            }
            else
            {
                // Standard single-request testing
                var fullUrl = BuildFullUrl(baseUrl, path, resolvedParams);
                var testResult = await ExecuteHttpRequestAsync(fullUrl, method, options, authentication, parsedResponseJsonByResult, cancellationToken, testedId);

                result.TestResults.Add(testResult);
                result.IsTested = true;

                // Check for non-success status codes and handle based on endpoint requirements
                if (!testResult.IsSuccessStatusCode)
                {
                    if (testResult.ResponseStatusCode == null && !string.IsNullOrEmpty(testResult.ErrorMessage))
                    {
                        result.Status = EndpointTestStatus.Error;
                    }
                    else
                    {
                        var isOptionalEndpoint = pathItem.IsOptionalEndpoint();
                        var statusCode = testResult.ResponseStatusCode ?? 0;
                        var errorMessage = $"Endpoint returned {statusCode} status code";

                        if (isOptionalEndpoint)
                        {
                            // For optional endpoints, add validation warning instead of error
                            testResult.ValidationResult ??= new ValidationResult
                            {
                                IsValid = false,
                                Errors = [],
                                SchemaVersion = string.Empty,
                                Duration = TimeSpan.Zero
                            };
                            testResult.ValidationResult.Errors.Add(new ValidationError
                            {
                                Path = path,
                                Message = $"Optional endpoint {method} {path} returned non-success status {statusCode}. This may indicate the endpoint is not implemented, which is acceptable for optional endpoints.",
                                ErrorCode = "OPTIONAL_ENDPOINT_NON_SUCCESS",
                                Severity = "Warning"
                            });
                            result.Status = EndpointTestStatus.PassedWithWarnings;
                        }
                        else
                        {
                            // For required endpoints, add validation error
                            testResult.ValidationResult ??= new ValidationResult
                            {
                                IsValid = false,
                                Errors = [],
                                SchemaVersion = string.Empty,
                                Duration = TimeSpan.Zero
                            };
                            testResult.ValidationResult.Errors.Add(new ValidationError
                            {
                                Path = path,
                                Message = $"Required endpoint {method} {path} returned non-success status {statusCode}. Expected 2xx status code.",
                                ErrorCode = "REQUIRED_ENDPOINT_FAILED",
                                Severity = "Error"
                            });
                            result.Status = EndpointTestStatus.FailedValidation;
                        }
                    }
                }

                // Validate response if schema is defined
                if (testResult.IsSuccessStatusCode && HasResponsePayload(testResult, parsedResponseJsonByResult))
                {
                    await ValidateResponseAsync(testResult, operation, openApiDocument, options, parsedResponseJsonByResult, cancellationToken);

                    var validationResult = testResult.ValidationResult;
                    if (validationResult == null || (validationResult.Errors.Count == 0 && !validationResult.IsValid))
                    {
                        // No schema was available for this response status, treat as pass.
                        result.Status = EndpointTestStatus.PassedValidation;
                    }
                    else if (validationResult.Errors.Any(e => string.Equals(e.Severity, "Warning", StringComparison.OrdinalIgnoreCase)) &&
                             !validationResult.Errors.Any(e => string.Equals(e.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Status = EndpointTestStatus.PassedWithWarnings;
                    }
                    else if (validationResult.IsValid)
                    {
                        result.Status = EndpointTestStatus.PassedValidation;
                    }
                    else
                    {
                        result.Status = EndpointTestStatus.FailedValidation;
                    }
                }


                // Optional endpoint warning logic (only apply if status wasn't already set by non-success handling)
                if (result.Status == EndpointTestStatus.NotTested || result.Status == EndpointTestStatus.PassedValidation || result.Status == EndpointTestStatus.FailedValidation)
                {
                    if (isOptional && (_openApiValidationOptions?.TestOptionalEndpoints ?? true) && (_openApiValidationOptions?.TreatOptionalEndpointsAsWarnings ?? true))
                    {
                        // If there are validation errors, report as warnings
                        if (testResult.ValidationResult != null && !testResult.ValidationResult.IsValid)
                        {
                            result.Status = EndpointTestStatus.PassedWithWarnings;
                        }
                        else if (result.Status != EndpointTestStatus.PassedWithWarnings)
                        {
                            result.Status = testResult.IsSuccessStatusCode
                                ? EndpointTestStatus.PassedValidation
                                : EndpointTestStatus.PassedWithWarnings;
                        }
                    }
                    else if (result.Status != EndpointTestStatus.PassedWithWarnings && result.Status != EndpointTestStatus.FailedValidation)
                    {
                        result.Status = testResult.IsSuccessStatusCode
                            ? EndpointTestStatus.PassedValidation
                            : EndpointTestStatus.FailedValidation;
                    }
                }

                NormalizeValidationResultErrors(testResult.ValidationResult);
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorTestingEndpoint(ex, TextSanitizer.SanitizeStringForLogging(method), TextSanitizer.SanitizeStringForLogging(path));
            result.TestResults.Add(new HttpTestResult
            {
                RequestUrl = $"{baseUrl}{path}",
                RequestMethod = method,
                IsSuccessStatusCode = false,
                ErrorMessage = TextSanitizer.SanitizeExceptionMessage(ex.Message),
                ResponseTime = TimeSpan.Zero
            });
            result.Status = EndpointTestStatus.Error;
        }
        finally
        {
            _ = semaphore.Release();
        }

        return result;
    }

    /// <summary>
    /// Tests a paginated endpoint by requesting the first page, last page, and a page in the middle.
    /// Validates pagination metadata and warns if the feed contains no data.
    /// </summary>
    private async Task TestPaginatedEndpointAsync(
        EndpointTestResult result,
        string path,
        string method,
        JsonObject operation,
        string baseUrl,
        OpenApiValidationOptions options,
        DataSourceAuthentication? auth,
        JsonArray resolvedParams,
        JsonObject openApiDocument,
        JsonObject pathItem,
        ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult,
        CancellationToken cancellationToken,
        bool retainFirstPageResponseJson = false)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.TestingPaginatedEndpoint(TextSanitizer.SanitizeStringForLogging(method), TextSanitizer.SanitizeStringForLogging(path));
        }

        result.IsTested = true;

        // Test first page (page=1)
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.TestingFirstPage(TextSanitizer.SanitizeForLogging(path));
        }
        var firstPageUrl = BuildFullUrl(baseUrl, path, resolvedParams, pageNumber: 1);
        var firstPageResult = await ExecuteHttpRequestAsync(firstPageUrl, method, options, auth, parsedResponseJsonByResult, cancellationToken);
        result.TestResults.Add(firstPageResult);

        if (!firstPageResult.IsSuccessStatusCode)
        {
            if (firstPageResult.ResponseStatusCode == null && !string.IsNullOrEmpty(firstPageResult.ErrorMessage))
            {
                result.Status = EndpointTestStatus.Error;
            }
            else
            {
                var isOptionalEndpoint = pathItem.IsOptionalEndpoint();
                var statusCode = firstPageResult.ResponseStatusCode ?? 0;

                if (isOptionalEndpoint)
                {
                    firstPageResult.ValidationResult!.Errors.Add(new ValidationError
                    {
                        Path = path,
                        Message = $"Optional endpoint {method} {path} returned non-success status {statusCode}. This may indicate the endpoint is not implemented, which is acceptable for optional endpoints.",
                        ErrorCode = "OPTIONAL_ENDPOINT_NON_SUCCESS",
                        Severity = "Warning"
                    });
                    NormalizeValidationResultErrors(firstPageResult.ValidationResult);
                    result.Status = EndpointTestStatus.PassedWithWarnings;
                }
                else
                {
                    firstPageResult.ValidationResult!.Errors.Add(new ValidationError
                    {
                        Path = path,
                        Message = $"Required endpoint {method} {path} returned non-success status {statusCode}. Expected 2xx status code.",
                        ErrorCode = "REQUIRED_ENDPOINT_FAILED",
                        Severity = "Error"
                    });
                    NormalizeValidationResultErrors(firstPageResult.ValidationResult);
                    result.Status = EndpointTestStatus.FailedValidation;
                }
            }
            return;
        }

        // Validate first page response schema
        if (HasResponsePayload(firstPageResult, parsedResponseJsonByResult))
        {
            await ValidateResponseAsync(firstPageResult, operation, openApiDocument, options, parsedResponseJsonByResult, cancellationToken);
        }

        // Try to determine total pages and check for empty feed
        var (totalPages, itemCount) = ExtractPaginationInfo(firstPageResult, parsedResponseJsonByResult);

        // Release the first page's parsed JSON document now that validation and pagination info extraction are complete,
        // unless we need to retain it for extracting IDs in dependency testing.
        if (!retainFirstPageResponseJson)
        {
            ReleaseParsedResponseJsonDocuments([firstPageResult], parsedResponseJsonByResult);
        }

        // Warn if feed returns no rows
        if (itemCount == 0)
        {
            firstPageResult.ValidationResult!.Errors.Add(new ValidationError
            {
                Path = path,
                Message = $"Paginated endpoint {method} {path} returned 0 items. Consider verifying if this is expected or if the feed should contain data.",
                ErrorCode = "EMPTY_FEED_WARNING",
                Severity = "Warning"
            });
            NormalizeValidationResultErrors(firstPageResult.ValidationResult);
            firstPageResult.ValidationResult.IsValid = false;
            result.Status = EndpointTestStatus.PassedWithWarnings;
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.PaginatedEndpointReturnedEmpty(TextSanitizer.SanitizeForLogging(path));
            }
            return; // No further pagination testing needed for empty feeds
        }

        if (totalPages.HasValue && totalPages.Value > 1)
        {
            var pages = totalPages.Value;
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.TestingPaginationPages(TextSanitizer.SanitizeForLogging(path), pages);
            }

            // Test middle page if there are more than 2 pages
            if (pages > 2)
            {
                var middlePage = pages / 2;
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.TestingMiddlePage(middlePage, TextSanitizer.SanitizeForLogging(path));
                }
                var middlePageUrl = BuildFullUrl(baseUrl, path, resolvedParams, pageNumber: middlePage);
                var middlePageResult = await ExecuteHttpRequestAsync(middlePageUrl, method, options, auth, parsedResponseJsonByResult, cancellationToken);
                result.TestResults.Add(middlePageResult);

                if (middlePageResult.IsSuccessStatusCode && HasResponsePayload(middlePageResult, parsedResponseJsonByResult))
                {
                    await ValidateResponseAsync(middlePageResult, operation, openApiDocument, options, parsedResponseJsonByResult, cancellationToken);
                }

                // Release the middle page's parsed JSON document after validation is complete.
                ReleaseParsedResponseJsonDocuments([middlePageResult], parsedResponseJsonByResult);
            }

            // Test last page
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.TestingLastPage(pages, TextSanitizer.SanitizeForLogging(path));
            }
            var lastPageUrl = BuildFullUrl(baseUrl, path, resolvedParams, pageNumber: pages);
            var lastPageResult = await ExecuteHttpRequestAsync(lastPageUrl, method, options, auth, parsedResponseJsonByResult, cancellationToken);
            result.TestResults.Add(lastPageResult);

            if (lastPageResult.IsSuccessStatusCode && HasResponsePayload(lastPageResult, parsedResponseJsonByResult))
            {
                await ValidateResponseAsync(lastPageResult, operation, openApiDocument, options, parsedResponseJsonByResult, cancellationToken);
            }

            // Release the last page's parsed JSON document after validation is complete.
            ReleaseParsedResponseJsonDocuments([lastPageResult], parsedResponseJsonByResult);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.SkippingAdditionalPageTests(TextSanitizer.SanitizeForLogging(path));
            }
        }

        foreach (var testResult in result.TestResults)
        {
            NormalizeValidationResultErrors(testResult.ValidationResult);
        }

        result.Status = DeterminePaginatedEndpointStatus(result);

        if (!ShouldRetainResponseBodies(options))
        {
            foreach (var tr in result.TestResults)
            {
                tr.ResponseBody = null;
            }
        }
    }

    private static EndpointTestStatus DeterminePaginatedEndpointStatus(EndpointTestResult result)
    {
        if (result.TestResults.Count == 0)
        {
            return EndpointTestStatus.NotTested;
        }

        if (result.TestResults.Any(tr => !tr.IsSuccessStatusCode))
        {
            return result.IsOptional
                ? EndpointTestStatus.PassedWithWarnings
                : EndpointTestStatus.FailedValidation;
        }

        var validationErrors = result.TestResults
            .Where(tr => tr.ValidationResult != null)
            .SelectMany(tr => tr.ValidationResult!.Errors);

        if (validationErrors.Any(e => string.Equals(e.Severity, "Error", StringComparison.OrdinalIgnoreCase)))
        {
            return EndpointTestStatus.FailedValidation;
        }

        if (validationErrors.Any(e => string.Equals(e.Severity, "Warning", StringComparison.OrdinalIgnoreCase)))
        {
            return EndpointTestStatus.PassedWithWarnings;
        }

        if (result.TestResults.Any(tr => tr.ValidationResult != null && !tr.ValidationResult.IsValid))
        {
            return EndpointTestStatus.FailedValidation;
        }

        return EndpointTestStatus.PassedValidation;
    }

    private static (int? TotalPages, int ItemCount) ExtractPaginationInfo(HttpTestResult response, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult)
    {
        if (parsedResponseJsonByResult.TryGetValue(response, out var parsedJson))
        {
            return ExtractPaginationInfo(parsedJson.RootElement);
        }

        return (null, 0);
    }

    private static (int? TotalPages, int ItemCount) ExtractPaginationInfo(JsonElement json)
    {
        int? totalPages = null;

        foreach (var path in TotalPagesPaths)
        {
            if (TryGetNestedPropertyIgnoreCase(json, path, out var totalPagesElement)
                && TryParseInt32(totalPagesElement, out var pages))
            {
                totalPages = pages;
                break;
            }
        }

        var itemCount = 0;
        if (json.ValueKind == JsonValueKind.Array)
        {
            itemCount = json.GetArrayLength();
        }
        else if (json.ValueKind == JsonValueKind.Object)
        {
            foreach (var propName in CollectionPropertyNames)
            {
                if (TryGetPropertyIgnoreCase(json, propName, out var itemsElement)
                    && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    itemCount = itemsElement.GetArrayLength();
                    break;
                }
            }

            if (itemCount == 0)
            {
                foreach (var propName in ItemCountPropertyNames)
                {
                    if (TryGetPropertyIgnoreCase(json, propName, out var sizeElement)
                        && TryParseInt32(sizeElement, out var size))
                    {
                        itemCount = size;
                        break;
                    }
                }
            }
        }

        return (totalPages, itemCount);
    }

    private string BuildFullUrl(string baseUrl, string path, JsonArray resolvedParams, int? pageNumber = null)
    {
        var url = $"{baseUrl.TrimEnd('/')}{path}";

        // Add page parameter if specified
        if (pageNumber.HasValue && HasPageParameter(resolvedParams))
        {
            var separator = url.Contains('?') ? "&" : "?";
            url += $"{separator}page={pageNumber.Value}";
        }

        return url;
    }

    /// <summary>
    /// Checks if the resolved parameters array contains a 'page' query parameter.
    /// Parameters should already be resolved (references expanded, path and operation params merged).
    /// </summary>
    private bool HasPageParameter(JsonArray resolvedParams)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.CheckingPageParameter(resolvedParams.Count);
        }
        foreach (var param in resolvedParams)
        {
            if (param is JsonObject paramObj)
            {
                var name = paramObj["name"]?.ToString();
                var inLocation = paramObj["in"]?.ToString();
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.CheckingParam(TextSanitizer.SanitizeStringForLogging(name ?? string.Empty), TextSanitizer.SanitizeStringForLogging(inLocation ?? string.Empty));
                }

                if (name?.Equals("page", StringComparison.OrdinalIgnoreCase) == true &&
                    inLocation?.Equals("query", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.FoundPageQueryParameter();
                    return true;
                }
            }
        }
        _logger.NoPageParameterFound();
        return false;
    }

    /// <summary>
    /// Merges path-level and operation-level parameters.
    /// Returns a JsonArray of parameter objects (references already resolved upstream).
    /// </summary>
    private JsonArray ResolveOperationParameters(JsonObject operation, JsonObject pathItem)
    {
        var resolvedParams = new JsonArray();

        // Add path-level parameters first (these are inherited by all operations)
        if (pathItem["parameters"] is JsonArray pathParams)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.FoundPathLevelParameters(pathParams.Count);
            }
            foreach (var param in pathParams)
            {
                resolvedParams.Add(param?.DeepClone());
                if (param is JsonObject paramObj)
                {
                    var paramName = paramObj["name"]?.ToString();
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.PathLevelParam(TextSanitizer.SanitizeStringForLogging(paramName ?? string.Empty));
                    }
                }
            }
        }

        // Add operation-level parameters (these can override path-level params)
        if (operation["parameters"] is JsonArray operationParams)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.FoundOperationLevelParameters(operationParams.Count);
            }
            foreach (var param in operationParams)
            {
                resolvedParams.Add(param?.DeepClone());
                if (param is JsonObject paramObj)
                {
                    var paramName = paramObj["name"]?.ToString();
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.OperationLevelParam(TextSanitizer.SanitizeStringForLogging(paramName ?? string.Empty));
                    }
                }
            }
        }

        _logger.TotalResolvedParameters(resolvedParams.Count);
        return resolvedParams;
    }

    private async Task<HttpTestResult> ExecuteHttpRequestAsync(string url, string method, OpenApiValidationOptions options, DataSourceAuthentication? authentication, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult, CancellationToken cancellationToken, string? testedId = null)
    {
        var testResult = new HttpTestResult
        {
            RequestUrl = url,
            RequestMethod = method,
            TestedId = testedId,
            ValidationResult = new ValidationResult()
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Add User-Agent header to match browser behavior
            // Many servers reject requests without a User-Agent header
            if (!request.Headers.Contains("User-Agent"))
            {
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            }

            // Apply request-supplied authentication only for HTTPS endpoints.
            // Never send user-provided credentials over plain HTTP.
            if (authentication != null &&
                Uri.TryCreate(url, UriKind.Absolute, out var requestUri) &&
                string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                ApplyAuthenticationHeaders(request, authentication);
            }
            else if (authentication != null)
            {
                _logger.SkippedAuthForNonHttpsEndpoint(TextSanitizer.SanitizeForLogging(url));
            }

            // Set timeout
            var timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // Use the injected HttpClient so test HttpMessageHandler mocks are respected.
            // Do NOT dispose - IHttpClientFactory manages the lifetime of pooled handlers.
            TimeSpan dnsLookup = TimeSpan.Zero, tcpConnection = TimeSpan.Zero, tlsHandshake = TimeSpan.Zero;
            var sendStart = Stopwatch.StartNew();
            var httpClient = _httpClientFactory.CreateClient(nameof(EndpointTestingService));
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var timeToHeaders = sendStart.Elapsed;

            // Stream response content to avoid materialising a full string on every request.
            // On the retain path (Full / IncludeResponseBody) we still need the string;
            // on the fast path we stream directly into a JsonDocument with no string allocation.
            byte[]? responseBody = null;
            JsonDocument? parsedResponseJson = null;
            var contentReadCanceled = false;
            var contentTransferStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (ShouldRetainResponseBodies(options))
                {
                    responseBody = await response.Content.ReadAsByteArrayAsync(cts.Token);
                    parsedResponseJson = TryParseJsonDocumentFromBuffer(responseBody);
                }
                else
                {
                    await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
                    parsedResponseJson = await TryParseJsonDocumentFromStreamAsync(contentStream, cts.Token);
                }
                contentTransferStopwatch.Stop();
            }
            catch (OperationCanceledException)
            {
                contentReadCanceled = true;
                contentTransferStopwatch.Stop();
            }

            if (contentReadCanceled)
            {
                sendStart.Stop();
                testResult.ResponseTime = timeToHeaders + contentTransferStopwatch.Elapsed;
                testResult.ResponseStatusCode = 408;
                testResult.IsSuccessStatusCode = false;
                testResult.ErrorMessage = "Response body read timed out or was canceled before completion.";
                testResult.PerformanceMetrics = new EndpointPerformanceMetrics
                {
                    DnsLookup = dnsLookup,
                    TcpConnection = tcpConnection,
                    TlsHandshake = tlsHandshake,
                    ServerProcessing = timeToHeaders,
                    ContentTransfer = contentTransferStopwatch.Elapsed
                };

                return testResult;
            }

            // Stop the overall timers
            sendStart.Stop();

            // Populate basic result fields
            testResult.ResponseTime = timeToHeaders + contentTransferStopwatch.Elapsed;
            testResult.ResponseStatusCode = (int)response.StatusCode;
            testResult.IsSuccessStatusCode = response.IsSuccessStatusCode;
            if (parsedResponseJson != null)
            {
                parsedResponseJsonByResult[testResult] = parsedResponseJson;
            }
            testResult.ResponseBody = responseBody;

            // Populate performance metrics (include best-effort DNS/TCP/TLS measurements if available)
            testResult.PerformanceMetrics = new EndpointPerformanceMetrics
            {
                DnsLookup = dnsLookup,
                TcpConnection = tcpConnection,
                TlsHandshake = tlsHandshake,
                ServerProcessing = timeToHeaders,
                ContentTransfer = contentTransferStopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            testResult.ResponseTime = stopwatch.Elapsed;
            testResult.IsSuccessStatusCode = false;
            testResult.ErrorMessage = TextSanitizer.SanitizeExceptionMessage(ex.Message);
        }

        return testResult;
    }

    private async Task ValidateResponseAsync(HttpTestResult testResult, JsonObject operation, JsonObject openApiDocument, OpenApiValidationOptions options, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult, CancellationToken cancellationToken)
    {
        try
        {
            if (operation["responses"] is JsonObject responsesObject)
            {
                var statusCode = testResult.ResponseStatusCode?.ToString() ?? "default";
                var responseSchema = responsesObject[statusCode] ?? responsesObject["default"];

                if (responseSchema is JsonObject responseSchemaObject
                    && responseSchemaObject["content"] is JsonObject contentObject)
                {
                    JsonObject? jsonContentObject = null;
                    foreach (var contentEntry in contentObject)
                    {
                        if (contentEntry.Key.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                            && contentEntry.Value is JsonObject candidate)
                        {
                            jsonContentObject = candidate;
                            break;
                        }
                    }

                    if (jsonContentObject?["schema"] is JsonNode schema)
                    {
                        var schemaForValidation = GetValidationSchemaForResponse(schema, openApiDocument);

                        object? jsonDataForValidation = null;
                        if (parsedResponseJsonByResult.TryGetValue(testResult, out var parsedJson))
                        {
                            jsonDataForValidation = parsedJson;
                        }
                        else if (testResult.ResponseBody != null && testResult.ResponseBody.Length > 0)
                        {
                            jsonDataForValidation = testResult.ResponseBody;
                        }

                        if (jsonDataForValidation is null)
                        {
                            return;
                        }

                        // Build schema in full OpenAPI context so internal refs like
                        // #/components/schemas/* are pre-resolved before runtime validation.
                        var validationRequest = new ValidationRequest
                        {
                            JsonData = jsonDataForValidation,
                            Schema = schemaForValidation,
                            Options = new ValidationOptions
                            {
                                MaxErrors = ResolveMaxValidationErrorsPerResponse(),
                                ReportAdditionalFields = (options?.ReportAdditionalFields ?? false)
                                    || ((_openApiValidationOptions?.OwnSchemaValidation
                                         ?? OwnSchemaValidationMode.Strict)
                                        == OwnSchemaValidationMode.Strict)
                            }
                        };
                        var validationResult = await _jsonValidatorService.ValidateAsync(validationRequest, cancellationToken);
                        _hsdsComplianceService.ApplyAdditionalFieldPolicy(validationResult, options?.ReportAdditionalFields ?? false);
                        testResult.ValidationResult = validationResult;
                        NormalizeValidationResultErrors(testResult.ValidationResult);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.CouldNotValidateResponse(ex, TextSanitizer.SanitizeUrlForLogging(testResult.RequestUrl ?? string.Empty));
        }
    }

    private JsonSchema GetValidationSchemaForResponse(JsonNode schema, JsonObject openApiDocument)
    {
        var schemaNode = schema.DeepClone();
        var cacheKey = ComputeSha256Hex(schemaNode.ToJsonString());
        return _validationSchemaCache.GetOrAdd(
            cacheKey,
            _ => BuildValidationSchemaWithComponentsContext(schemaNode, openApiDocument));
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static (
        int ParsedJsonDocumentsInFlight,
        int RetainedResponseBodies,
        long RetainedResponseBodyChars,
        int ExtractedIdRoots,
        int ExtractedIdValues,
        int ValidationSchemaCacheEntries) GetEndpointRetentionSnapshot(
            IEnumerable<EndpointTestResult> endpointResults,
            ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult,
            ConcurrentDictionary<string, List<string>> extractedIds,
            ConcurrentDictionary<string, JsonSchema> validationSchemaCache)
    {
        int retainedResponseBodies = 0;
        long retainedResponseBodyChars = 0;

        foreach (var endpointResult in endpointResults)
        {
            foreach (var httpResult in endpointResult.TestResults)
            {
                if (httpResult.ResponseBody == null || httpResult.ResponseBody.Length == 0)
                {
                    continue;
                }

                retainedResponseBodies++;
                retainedResponseBodyChars += httpResult.ResponseBody.Length;
            }
        }

        return (
            parsedResponseJsonByResult.Count,
            retainedResponseBodies,
            retainedResponseBodyChars,
            extractedIds.Count,
            extractedIds.Values.Sum(static ids => ids.Count),
            validationSchemaCache.Count);
    }

    private static async Task<JsonDocument?> TryParseJsonDocumentFromStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static JsonDocument? TryParseJsonDocumentFromBuffer(byte[] buffer)
    {
        try
        {
            return JsonDocument.Parse(buffer.AsMemory());
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetNestedPropertyIgnoreCase(JsonElement element, IEnumerable<string> pathSegments, out JsonElement value)
    {
        var current = element;
        foreach (var segment in pathSegments)
        {
            if (!TryGetPropertyIgnoreCase(current, segment, out current))
            {
                value = default;
                return false;
            }
        }

        value = current;
        return true;
    }

    private static bool TryParseInt32(JsonElement element, out int value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt32(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(element.GetString(), out value);
        }

        value = 0;
        return false;
    }

    private static bool HasResponsePayload(HttpTestResult response, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult)
    {
        return parsedResponseJsonByResult.ContainsKey(response) || (response.ResponseBody != null && response.ResponseBody.Length > 0);
    }

    private static void ReleaseParsedResponseJsonDocuments(IEnumerable<HttpTestResult> testResults, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult)
    {
        foreach (var testResult in testResults)
        {
            if (parsedResponseJsonByResult.TryRemove(testResult, out var parsedJson))
            {
                parsedJson.Dispose();
            }
        }
    }

    private static JsonSchema BuildValidationSchemaWithComponentsContext(JsonNode schema, JsonObject openApiDocument)
    {
        JsonNode schemaToCompile = schema.DeepClone();

        if (RequiresComponentsContext(schema)
            && openApiDocument["components"] is JsonObject components
            && schema is JsonObject schemaObject)
        {
            // Keep the response schema at document root and attach components so refs like
            // #/components/schemas/* remain resolvable without introducing synthetic wrapper refs.
            var schemaWithComponents = (JsonObject)schemaObject.DeepClone();
            if (!schemaWithComponents.ContainsKey("components"))
            {
                schemaWithComponents["components"] = components.DeepClone();
            }

            schemaToCompile = schemaWithComponents;
        }

        return JsonSchemaBuild.FromText(schemaToCompile.ToJsonString());
    }

    private static bool RequiresComponentsContext(JsonNode schema)
    {
        if (schema is JsonObject schemaObject
            && schemaObject.TryGetPropertyValue("$ref", out var refToken)
            && refToken is not null
            && refToken.ToString().StartsWith("#/components/", StringComparison.Ordinal))
        {
            return true;
        }

        if (schema is JsonObject objectNode)
        {
            foreach (var child in objectNode)
            {
                if (child.Value is not null && RequiresComponentsContext(child.Value))
                {
                    return true;
                }
            }
        }
        else if (schema is JsonArray arrayNode)
        {
            foreach (var child in arrayNode)
            {
                if (child is not null && RequiresComponentsContext(child))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void NormalizeValidationResultErrors(ValidationResult? validationResult)
    {
        if (validationResult?.Errors == null || validationResult.Errors.Count == 0)
        {
            return;
        }

        validationResult.Errors = ValidationErrorNormalizer.NormalizeAndDeduplicateByPath(validationResult.Errors);
    }

    private bool ShouldRetainResponseBodies(OpenApiValidationOptions options)
    {
        return options.IncludeResponseBody
            || (_openApiValidationOptions?.HsdsValidationMode == HsdsValidationMode.Full);
    }

    private int ResolveMaxValidationErrorsPerResponse()
    {
        var configuredMaxErrors = _openApiValidationOptions?.MaxValidationErrorsPerResponse;
        return configuredMaxErrors.HasValue && configuredMaxErrors.Value > 0
            ? configuredMaxErrors.Value
            : 100;
    }

    private static List<EndpointGroup> GroupEndpointsByDependencies(JsonObject pathsObject)
    {
        var endpoints = new List<EndpointInfo>();

        // Extract all endpoints
        foreach (var pathProperty in pathsObject)
        {
            var path = pathProperty.Key;
            var pathItem = pathProperty.Value;

            if (pathItem is JsonObject pathItemObject)
            {
                foreach (var methodProperty in pathItemObject)
                {
                    var method = methodProperty.Key.ToUpperInvariant();

                    // Skip non-HTTP method properties like "parameters", "summary", "$ref", "servers", etc.
                    if (!ValidHttpMethods.Contains(method))
                    {
                        continue;
                    }

                    var operation = methodProperty.Value;
                    if (operation is JsonObject operationObject)
                    {
                        endpoints.Add(new EndpointInfo
                        {
                            Path = path,
                            Method = method,
                            Operation = (JsonObject)operationObject.DeepClone(),
                            PathItem = (JsonObject)pathItemObject.DeepClone()  // Add path item for optional endpoint checking
                        });
                    }
                }
            }
        }

        // Group by root path and separate collection from parameterized
        var groups = endpoints
            .GroupBy(e => e.RootPath)
            .Select(g => new EndpointGroup
            {
                RootPath = g.Key,
                CollectionEndpoints = [.. g.Where(e => !e.IsParameterized && e.Method == "GET")],
                ParameterizedEndpoints = [.. g.Where(e => e.IsParameterized)]
            })
            .Where(g => g.Endpoints.Count > 0)
            .ToList();

        return groups;
    }

    /// <summary>
    /// Tests an endpoint and extracts IDs from the response for use by dependent endpoints.
    /// The extractedIds dictionary is updated with any IDs found in the response.
    /// </summary>
    /// <param name="path">The endpoint path to test</param>
    /// <param name="method">The HTTP method to use</param>
    /// <param name="operation">The OpenAPI operation definition</param>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="authentication">Authentication configuration</param>
    /// <param name="options">Validation options</param>
    /// <param name="extractedIds">Dictionary to store extracted IDs (passed by reference, modifications persist)</param>
    /// <param name="semaphore">Semaphore for concurrency control</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The endpoint test result with extracted IDs stored in the shared dictionary</returns>
    private async Task<EndpointTestResult> TestSingleEndpointWithIdExtractionAsync(
        string path, string method, JsonObject operation, string baseUrl,
        OpenApiValidationOptions options, DataSourceAuthentication? authentication,
        ConcurrentDictionary<string, List<string>> extractedIds, SemaphoreSlim semaphore,
        JsonObject openApiDocument, JsonObject pathItem, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult, CancellationToken cancellationToken)
    {
        var result = await TestSingleEndpointAsync(path, method, operation, baseUrl, options, authentication, semaphore, openApiDocument, pathItem, parsedResponseJsonByResult, cancellationToken, retainResponseJson: true);

        // Extract IDs from successful GET responses for dependency testing
        if (method == "GET" && result.TestResults.Any(r => r.IsSuccessStatusCode && HasResponsePayload(r, parsedResponseJsonByResult)))
        {
            var rootPath = EndpointInfo.GetRootPath(path);
            var successfulResponse = result.TestResults.First(r => r.IsSuccessStatusCode);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.ProcessingHttpResponse(
                    TextSanitizer.SanitizeUrlForLogging(successfulResponse.RequestUrl ?? string.Empty),
                    successfulResponse.ResponseStatusCode ?? 0,
                    successfulResponse.ResponseBody?.Length ?? 0);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.ResponseContentLength(successfulResponse.ResponseBody?.Length ?? 0);
            }

            var ids = ExtractIdsFromResponse(successfulResponse, rootPath, operation, openApiDocument, parsedResponseJsonByResult);

            if (ids.Count > 0)
            {
                // Store extracted IDs in the shared dictionary for use by dependent endpoints
                // Note: ConcurrentDictionary is a reference type, so this modification persists to the caller
                extractedIds[rootPath] = ids;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.SuccessfullyExtractedIds(ids.Count, TextSanitizer.SanitizeForLogging(path), TextSanitizer.SanitizeForLogging(rootPath));
                }

                // Verify the IDs were stored correctly
                if (extractedIds.TryGetValue(rootPath, out var storedIds))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.VerifiedIdsStored(storedIds.Count, TextSanitizer.SanitizeForLogging(rootPath));
                    }
                }
                else
                {
                    _logger.IdsVerificationFailed(TextSanitizer.SanitizeForLogging(rootPath));
                }
            }
            else
            {
                _logger.NoIdsExtracted(TextSanitizer.SanitizeForLogging(path), TextSanitizer.SanitizeForLogging(rootPath));
            }
        }

        if (!ShouldRetainResponseBodies(options))
        {
            foreach (var tr in result.TestResults)
            {
                tr.ResponseBody = null;
            }
        }

        ReleaseParsedResponseJsonDocuments(result.TestResults, parsedResponseJsonByResult);

        return result;
    }

    /// <summary>
    /// Tests an endpoint with parameter substitution using extracted IDs from the shared dictionary.
    /// This method retrieves IDs extracted by TestSingleEndpointWithIdExtractionAsync and uses them
    /// to test parameterized endpoints with realistic data.
    /// </summary>
    /// <param name="path">The parameterized endpoint path to test</param>
    /// <param name="method">The HTTP method to use</param>
    /// <param name="operation">The OpenAPI operation definition</param>
    /// <param name="baseUrl">The base URL for the API</param>
    /// <param name="authentication">Authentication configuration</param>
    /// <param name="options">Validation options</param>
    /// <param name="extractedIds">Dictionary containing extracted IDs from collection endpoints</param>
    /// <param name="semaphore">Semaphore for concurrency control</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The endpoint test result using extracted IDs for parameters</returns>
    private async Task<EndpointTestResult> TestSingleEndpointWithIdSubstitutionAsync(
        string path, string method, JsonObject operation, string baseUrl,
        OpenApiValidationOptions options, DataSourceAuthentication? authentication,
        ConcurrentDictionary<string, List<string>> extractedIds, SemaphoreSlim semaphore,
        JsonObject openApiDocument, JsonObject pathItem, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult, CancellationToken cancellationToken)
    {
        var rootPath = EndpointInfo.GetRootPath(path);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LookingForExtractedIds(TextSanitizer.SanitizeForLogging(rootPath), extractedIds.Keys.Count);
        }

        // Try to retrieve extracted IDs from the shared dictionary populated by collection endpoint tests
        if (extractedIds.TryGetValue(rootPath, out var availableIds) && availableIds.Count > 0)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.FoundExtractedIds(availableIds.Count, TextSanitizer.SanitizeForLogging(rootPath));
            }

            // Test up to 10 random IDs from the available IDs
            var maxIdsToTest = Math.Min(10, availableIds.Count);
            var random = new Random();
            List<string> idsToTest = availableIds.Count <= 10
                ? [.. availableIds]
                : [.. availableIds.OrderBy(_ => random.Next()).Take(10)];

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.TestingRandomIds(idsToTest.Count, TextSanitizer.SanitizeForLogging(path));
            }

            // Create a composite result that combines all test results
            var compositeResult = new EndpointTestResult
            {
                Path = path,
                Method = method,
                Name = operation["name"]?.ToString(),
                OperationId = operation["operationId"]?.ToString(),
                Summary = operation["summary"]?.ToString(),
                IsOptional = operation.IsOptionalEndpoint(),
                Status = EndpointTestStatus.NotTested,
                IsTested = false
            };

            // Test each ID
            var allTestsSuccessful = true;
            var hasSkippedResult = false;
            foreach (var id in idsToTest)
            {
                var substitutedPath = SubstitutePathParametersWithSpecificId(path, id);
                _logger.TestingEndpointWithExtractedId();

                var singleResult = await TestSingleEndpointAsync(substitutedPath, method, operation, baseUrl, options, authentication, semaphore, openApiDocument, pathItem, parsedResponseJsonByResult, cancellationToken, testedId: id);

                // Aggregate the results
                compositeResult.TestResults.AddRange(singleResult.TestResults);
                //compositeResult.ValidationErrors.AddRange(singleResult.ValidationErrors);
                //compositeResult.SchemaValidationDetails.AddRange(singleResult.SchemaValidationDetails);

                if (singleResult.Status == EndpointTestStatus.Skipped)
                {
                    hasSkippedResult = true;
                }

                if (singleResult.Status == EndpointTestStatus.FailedValidation || singleResult.Status == EndpointTestStatus.Error)
                {
                    allTestsSuccessful = false;
                }

                // Release parsed JSON documents immediately after each individual ID test to
                // avoid accumulating all responses in memory simultaneously.
                ReleaseParsedResponseJsonDocuments(singleResult.TestResults, parsedResponseJsonByResult);
            }

            compositeResult.IsTested = compositeResult.TestResults.Count > 0;

            // Set the composite status based on all test results
            if (compositeResult.TestResults.Count > 0)
            {
                if (allTestsSuccessful)
                {
                    compositeResult.Status = EndpointTestStatus.PassedValidation;
                }
                else if (compositeResult.TestResults.Any(tr => tr.ValidationResult != null && tr.ValidationResult.Errors.Any(e => e.Severity == "Warning")) && !compositeResult.TestResults.Any(tr => tr.ValidationResult != null && tr.ValidationResult.Errors.Any(e => e.Severity == "Error")))
                {
                    compositeResult.Status = EndpointTestStatus.PassedWithWarnings;
                }
                else
                {
                    compositeResult.Status = EndpointTestStatus.FailedValidation;
                }
            }
            else if (hasSkippedResult)
            {
                compositeResult.Status = EndpointTestStatus.Skipped;
            }

            if (!ShouldRetainResponseBodies(options))
            {
                foreach (var tr in compositeResult.TestResults)
                {
                    tr.ResponseBody = null;
                }
            }

            return compositeResult;
        }
        else
        {
            _logger.NoExtractedIdsAvailable(TextSanitizer.SanitizeForLogging(rootPath), extractedIds.Count, TextSanitizer.SanitizeForLogging(path));

            // Log available keys for debugging
            if (!extractedIds.IsEmpty)
            {
                _logger.AvailableIdKeysCount(extractedIds.Keys.Count);
            }

            // Return a NotTested result instead of falling back to default values
            var notTestedResult = new EndpointTestResult
            {
                Path = path,
                Method = method,
                Name = operation["name"]?.ToString(),
                OperationId = operation["operationId"]?.ToString(),
                Summary = operation["summary"]?.ToString(),
                IsOptional = operation.IsOptionalEndpoint(),
                Status = EndpointTestStatus.NotTested,
                IsTested = false,
                TestResults =
                [
                    new() {
                        IsSuccessStatusCode = false,
                        RequestMethod = method,
                        RequestUrl = $"{baseUrl}{path}",
                        ErrorMessage = "No extracted IDs available for parameter substitution. Endpoint was not tested.",
                        ValidationResult = new ValidationResult
                        {
                            IsValid = false,
                            Errors =
                            [
                                new()
                                {
                                    Path = path,
                                    Message = "No extracted IDs available for parameter substitution. Endpoint was not tested.",
                                    ErrorCode = "NO_IDS_AVAILABLE",
                                    Severity = "Warning"
                                }
                            ]
                        }
                    }
                ]
            };

            NormalizeValidationResultErrors(notTestedResult.TestResults.FirstOrDefault()?.ValidationResult);
            return notTestedResult;
        }
    }

    /// <summary>
    /// Extracts IDs from a JSON response using OpenAPI schema information to identify ID field locations
    /// </summary>
    private List<string> ExtractIdsFromResponse(HttpTestResult response, string rootPath, JsonObject operation, JsonObject openApiDocument, ConcurrentDictionary<HttpTestResult, JsonDocument> parsedResponseJsonByResult)
    {
        var ids = new List<string>();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.StartingIdExtraction(TextSanitizer.SanitizeForLogging(rootPath));
        }

        // First, try to extract ID field names from the OpenAPI schema
        var schemaIdFields = ExtractIdFieldsFromSchema(operation, openApiDocument);
        if (schemaIdFields.Count > 0)
        {
            _logger.FoundIdFieldsFromSchema(schemaIdFields.Count);
        }
        else
        {
            _logger.FallingBackToCommonFieldNames();
        }

        if (parsedResponseJsonByResult.TryGetValue(response, out var parsedJson))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.ParsedJsonType(parsedJson.RootElement.ValueKind.ToString());
            }
            try
            {
                ExtractIdsFromJsonElement(parsedJson.RootElement, schemaIdFields, operation, openApiDocument, ids);
            }
            catch (Exception ex)
            {
                _logger.FailedToExtractIds(ex, TextSanitizer.SanitizeForLogging(rootPath));
            }
        }

        return [.. ids.Distinct()];
    }

    private void ExtractIdsFromJsonElement(JsonElement json, List<string> schemaIdFields, JsonObject operation, JsonObject openApiDocument, List<string> ids)
    {
        if (json.ValueKind == JsonValueKind.Array)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.FoundJsonArray(json.GetArrayLength());
            }

            foreach (var item in json.EnumerateArray())
            {
                var id = ExtractIdFromElement(item, schemaIdFields);
                if (!string.IsNullOrEmpty(id))
                {
                    _logger.FoundIdInArrayItem();
                    ids.Add(id);
                }
            }

            return;
        }

        if (json.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var collectionProps = ExtractCollectionPropertiesFromSchema(operation, openApiDocument);
        if (collectionProps.Count > 0)
        {
            var sanitizedProps = string.Join(", ", collectionProps.Select(p => TextSanitizer.SanitizeForLogging(p)));
            _logger.FoundCollectionProperties(sanitizedProps);

            foreach (var propName in collectionProps)
            {
                if (TryGetPropertyIgnoreCase(json, propName, out var itemsElement)
                    && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.ProcessingCollectionProperty(TextSanitizer.SanitizeForLogging(propName), itemsElement.GetArrayLength());
                    }
                    foreach (var item in itemsElement.EnumerateArray())
                    {
                        var id = ExtractIdFromElement(item, schemaIdFields);
                        if (!string.IsNullOrEmpty(id))
                        {
                            ids.Add(id);
                        }
                    }

                    break;
                }
            }
        }

        if (ids.Count == 0)
        {
            foreach (var propName in CollectionPropertyNames)
            {
                if (TryGetPropertyIgnoreCase(json, propName, out var itemsElement)
                    && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.ProcessingFallbackCollectionProperty(TextSanitizer.SanitizeForLogging(propName), itemsElement.GetArrayLength());
                    }
                    foreach (var item in itemsElement.EnumerateArray())
                    {
                        var id = ExtractIdFromElement(item, schemaIdFields);
                        if (!string.IsNullOrEmpty(id))
                        {
                            ids.Add(id);
                        }
                    }

                    break;
                }
            }
        }

        if (ids.Count == 0)
        {
            var id = ExtractIdFromElement(json, schemaIdFields);
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }
        }
    }

    private static string? ExtractIdFromElement(JsonElement item, List<string> schemaIdFields)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var fieldName in schemaIdFields)
        {
            if (TryGetPropertyIgnoreCase(item, fieldName, out var fieldValue)
                && fieldValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                var value = fieldValue.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        foreach (var fallbackName in FallbackIdNames)
        {
            if (TryGetPropertyIgnoreCase(item, fallbackName, out var fieldValue)
                && fieldValue.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                var value = fieldValue.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static JsonNode? ResolveLocalRef(string refStr, JsonObject openApiDocument)
    {
        if (string.IsNullOrWhiteSpace(refStr) || !refStr.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = refStr[2..].Split('/');
        JsonNode? current = openApiDocument;

        foreach (var part in parts)
        {
            var decodedPart = Uri.UnescapeDataString(part).Replace("~1", "/").Replace("~0", "~");
            if (current is JsonObject obj && obj.TryGetPropertyValue(decodedPart, out var nextNode))
            {
                current = nextNode;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Extracts ID field names from the OpenAPI response schema
    /// </summary>
    private List<string> ExtractIdFieldsFromSchema(JsonObject operation, JsonObject openApiDocument)
    {
        var idFields = new List<string>();

        try
        {
            // Get the 200 response schema
            var responseSchema = operation["responses"]?["200"]?["content"]?["application/json"]?["schema"];
            if (responseSchema != null)
            {
                ExtractIdFieldsFromSchemaRecursive(responseSchema, idFields, openApiDocument, new HashSet<JsonNode>(System.Collections.Generic.ReferenceEqualityComparer.Instance));
            }
        }
        catch (Exception ex)
        {
            _logger.FailedToExtractIdFieldsFromSchema(ex);
        }

        return [.. idFields.Distinct()];
    }

    /// <summary>
    /// Extracts collection property names from the OpenAPI response schema
    /// </summary>
    private List<string> ExtractCollectionPropertiesFromSchema(JsonObject operation, JsonObject openApiDocument)
    {
        var collectionProps = new List<string>();

        try
        {
            // Get the 200 response schema
            var responseSchema = operation["responses"]?["200"]?["content"]?["application/json"]?["schema"];
            if (responseSchema != null)
            {
                ExtractCollectionPropertiesFromSchemaRecursive(responseSchema, collectionProps, openApiDocument, new HashSet<JsonNode>(System.Collections.Generic.ReferenceEqualityComparer.Instance));
            }
        }
        catch (Exception ex)
        {
            _logger.FailedToExtractCollectionProperties(ex);
        }

        return [.. collectionProps.Distinct()];
    }

    /// <summary>
    /// Recursively extracts ID field names from a schema structure
    /// </summary>
    private static void ExtractIdFieldsFromSchemaRecursive(JsonNode schema, List<string> idFields, JsonObject openApiDocument, HashSet<JsonNode> visited)
    {
        if (schema == null || !visited.Add(schema))
        {
            return;
        }

        if (schema is JsonObject schemaObj)
        {
            // Check if this schema has a reference
            if (schemaObj.TryGetPropertyValue("$ref", out var refToken) && refToken is not null)
            {
                var refStr = refToken.ToString();
                var resolved = ResolveLocalRef(refStr, openApiDocument);
                if (resolved != null)
                {
                    ExtractIdFieldsFromSchemaRecursive(resolved, idFields, openApiDocument, visited);
                }
                return;
            }

            // Check if this schema has properties
            if (schemaObj["properties"] is JsonObject properties)
            {
                foreach (var prop in properties)
                {
                    var propName = prop.Key;
                    var propSchema = prop.Value;

                    // Check if this looks like an ID field
                    if (IsIdField(propName, propSchema))
                    {
                        idFields.Add(propName);
                    }

                    // Recursively check nested properties
                    if (propSchema != null)
                    {
                        ExtractIdFieldsFromSchemaRecursive(propSchema, idFields, openApiDocument, visited);
                    }
                }
            }

            // Check array items
            if (schemaObj["items"] is JsonNode itemsSchema)
            {
                ExtractIdFieldsFromSchemaRecursive(itemsSchema, idFields, openApiDocument, visited);
            }

            // Check allOf, anyOf, oneOf
            foreach (var combiner in SchemaCombiners)
            {
                if (schemaObj[combiner] is JsonArray combinerArray)
                {
                    foreach (var item in combinerArray)
                    {
                        if (item != null)
                        {
                            ExtractIdFieldsFromSchemaRecursive(item, idFields, openApiDocument, visited);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recursively extracts collection property names from a schema structure
    /// </summary>
    private static void ExtractCollectionPropertiesFromSchemaRecursive(JsonNode schema, List<string> collectionProps, JsonObject openApiDocument, HashSet<JsonNode> visited)
    {
        if (schema == null || !visited.Add(schema))
        {
            return;
        }

        if (schema is JsonObject schemaObj)
        {
            // Check if this schema has a reference
            if (schemaObj.TryGetPropertyValue("$ref", out var refToken) && refToken is not null)
            {
                var refStr = refToken.ToString();
                var resolved = ResolveLocalRef(refStr, openApiDocument);
                if (resolved != null)
                {
                    ExtractCollectionPropertiesFromSchemaRecursive(resolved, collectionProps, openApiDocument, visited);
                }
                return;
            }

            // Check if this schema has properties
            if (schemaObj["properties"] is JsonObject properties)
            {
                foreach (var prop in properties)
                {
                    var propName = prop.Key;
                    var propSchema = prop.Value;

                    // Check if this property is an array (collection)
                    if (propSchema is JsonObject propObj && propObj["type"]?.ToString() == "array")
                    {
                        collectionProps.Add(propName);
                    }

                    // Recursively check nested properties
                    if (propSchema != null)
                    {
                        ExtractCollectionPropertiesFromSchemaRecursive(propSchema, collectionProps, openApiDocument, visited);
                    }
                }
            }

            // Check allOf, anyOf, oneOf
            foreach (var combiner in new[] { "allOf", "anyOf", "oneOf" })
            {
                if (schemaObj[combiner] is JsonArray combinerArray)
                {
                    foreach (var item in combinerArray)
                    {
                        if (item != null)
                        {
                            ExtractCollectionPropertiesFromSchemaRecursive(item, collectionProps, openApiDocument, visited);
                        }
                    }
                }
            }
        }
    }


    /// <summary>
    /// Determines if a property name and schema indicate an ID field
    /// </summary>
    private static bool IsIdField(string propName, JsonNode? propSchema)
    {
        // Check property name patterns
        var nameLower = propName.ToLowerInvariant();
        if (nameLower == "id" || nameLower == "_id" || nameLower == "uid" ||
            nameLower == "uuid" || nameLower == "identifier" || nameLower == "key" ||
            nameLower.EndsWith("id") || nameLower.EndsWith("_id"))
        {
            return true;
        }

        // Check schema properties for ID indicators
        if (propSchema is JsonObject schemaObj)
        {
            var description = schemaObj["description"]?.ToString().ToLowerInvariant();
            if (!string.IsNullOrEmpty(description) &&
                (description.Contains("identifier") || description.Contains("unique id") || description.Contains(" id ")))
            {
                return true;
            }

            var format = schemaObj["format"]?.ToString().ToLowerInvariant();
            if (format == "uuid" || format == "guid")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Substitutes path parameters with a specific ID value
    /// </summary>
    private static string SubstitutePathParametersWithSpecificId(string path, string id)
    {
        var substitutedPath = path;

        // Find all path parameters and replace with the specific ID
        var matches = PathParameterRegex().Matches(path);

        foreach (Match match in matches)
        {
            var paramPlaceholder = match.Value;
            substitutedPath = substitutedPath.Replace(paramPlaceholder, id);
        }

        return substitutedPath;
    }

    private static bool IsValidHttpHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        const string allowedHeaderTokenSymbols = "!#$%&'*+-.^_`|~";

        foreach (var c in headerName)
        {
            if (char.IsLetterOrDigit(c))
            {
                continue;
            }

            if (allowedHeaderTokenSymbols.Contains(c))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsSafeHeaderValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c == '\r' || c == '\n' || char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies authentication to an HTTP request based on the provided authentication configuration
    /// Supports API key, bearer token, basic authentication, and custom headers
    /// </summary>
    /// <param name="request">The HTTP request message to apply authentication to</param>
    /// <param name="authentication">The authentication configuration containing credentials and auth type</param>
    private void ApplyAuthenticationHeaders(HttpRequestMessage request, DataSourceAuthentication authentication)
    {
        // Apply API Key authentication
        if (!string.IsNullOrEmpty(authentication.ApiKey))
        {
            var headerName = string.IsNullOrEmpty(authentication.ApiKeyHeader) ? "X-API-Key" : authentication.ApiKeyHeader;
            if (IsValidHttpHeaderName(headerName) && IsSafeHeaderValue(authentication.ApiKey))
            {
                request.Headers.Add(headerName, authentication.ApiKey);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.AppliedApiKeyAuthenticationWithHeader(TextSanitizer.SanitizeForLogging(headerName));
                }
            }
            else
            {
                _logger.SkippedApiKeyAuthentication();
            }
        }

        // Apply Bearer Token authentication
        if (!string.IsNullOrEmpty(authentication.BearerToken))
        {
            if (IsSafeHeaderValue(authentication.BearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authentication.BearerToken);
                EndpointTestingLog.AppliedBearerTokenAuthentication(_logger);
            }
            else
            {
                _logger.SkippedBearerTokenAuthentication();
            }
        }

        // Apply Basic Authentication
        if (authentication.BasicAuth != null &&
            !string.IsNullOrEmpty(authentication.BasicAuth.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{authentication.BasicAuth.Username}:{authentication.BasicAuth.Password ?? string.Empty}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.AppliedBasicAuthentication(TextSanitizer.SanitizeForLogging(authentication.BasicAuth.Username));
            }
        }

        // Apply Custom Headers
        if (authentication.CustomHeaders != null && authentication.CustomHeaders.Count > 0)
        {
            foreach (var header in authentication.CustomHeaders)
            {
                if (!string.IsNullOrEmpty(header.Key) &&
                    !string.IsNullOrEmpty(header.Value) &&
                    IsValidHttpHeaderName(header.Key) &&
                    IsSafeHeaderValue(header.Value))
                {
                    request.Headers.Add(header.Key, header.Value);
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        EndpointTestingLog.AppliedCustomHeader(_logger, TextSanitizer.SanitizeForLogging(header.Key));
                    }
                }
                else
                {
                    _logger.SkippedInvalidCustomHeader();
                }
            }
        }
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex PathParameterRegex();
}
