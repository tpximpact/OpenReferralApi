using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class EndpointTestingLog
    {
        [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Testing OpenAPI endpoints with intelligent dependency ordering")]
        public static partial void TestingEndpointsWithDependencyOrdering(this ILogger logger);

        [LoggerMessage(EventId = 5001, Level = LogLevel.Warning, Message = "No paths found in OpenAPI specification")]
        public static partial void NoPathsFound(this ILogger logger);

        [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Found {GroupCount} endpoint groups for dependency-aware testing")]
        public static partial void FoundEndpointGroups(this ILogger logger, int groupCount);

        [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Testing endpoint group: {GroupName} with {Count} endpoints")]
        public static partial void TestingEndpointGroup(this ILogger logger, string groupName, int count);

        [LoggerMessage(EventId = 5004, Level = LogLevel.Information, Message = "Completed group {GroupName}: {CollectionCount} collection + {ParamCount} parameterized endpoints")]
        public static partial void CompletedEndpointGroup(this ILogger logger, string groupName, int collectionCount, int paramCount);

        [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "Completed testing {Count} endpoints with intelligent dependency ordering")]
        public static partial void CompletedTestingEndpoints(this ILogger logger, int count);

        [LoggerMessage(EventId = 5006, Level = LogLevel.Error, Message = "Error during dependency-aware endpoint testing")]
        public static partial void ErrorDuringEndpointTesting(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 5007, Level = LogLevel.Debug, Message = "Checking pagination support for {Method} {Path}")]
        public static partial void CheckingPaginationSupport(this ILogger logger, string method, string path);

        [LoggerMessage(EventId = 5008, Level = LogLevel.Information, Message = "{Method} {Path}: hasPagination={HasPagination}")]
        public static partial void PaginationCheckResult(this ILogger logger, string method, string path, bool hasPagination);

        [LoggerMessage(EventId = 5009, Level = LogLevel.Error, Message = "Error testing endpoint {Method} {Path}")]
        public static partial void ErrorTestingEndpoint(this ILogger logger, Exception exception, string method, string path);

        [LoggerMessage(EventId = 5010, Level = LogLevel.Information, Message = "Testing paginated endpoint: {Method} {Path}")]
        public static partial void TestingPaginatedEndpoint(this ILogger logger, string method, string path);

        [LoggerMessage(EventId = 5011, Level = LogLevel.Debug, Message = "Testing first page for {Path}")]
        public static partial void TestingFirstPage(this ILogger logger, string path);

        [LoggerMessage(EventId = 5012, Level = LogLevel.Warning, Message = "Paginated endpoint {Path} returned empty feed (0 items)")]
        public static partial void PaginatedEndpointReturnedEmpty(this ILogger logger, string path);

        [LoggerMessage(EventId = 5013, Level = LogLevel.Information, Message = "Endpoint {Path} has {TotalPages} pages, testing pagination")]
        public static partial void TestingPaginationPages(this ILogger logger, string path, int totalPages);

        [LoggerMessage(EventId = 5014, Level = LogLevel.Debug, Message = "Testing middle page {PageNumber} for {Path}")]
        public static partial void TestingMiddlePage(this ILogger logger, int pageNumber, string path);

        [LoggerMessage(EventId = 5015, Level = LogLevel.Debug, Message = "Testing last page {PageNumber} for {Path}")]
        public static partial void TestingLastPage(this ILogger logger, int pageNumber, string path);

        [LoggerMessage(EventId = 5016, Level = LogLevel.Debug, Message = "Endpoint {Path} has only 1 page or pagination info not available, skipping additional page tests")]
        public static partial void SkippingAdditionalPageTests(this ILogger logger, string path);

        [LoggerMessage(EventId = 5017, Level = LogLevel.Debug, Message = "Failed to extract pagination info from response")]
        public static partial void FailedToExtractPaginationInfo(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 5018, Level = LogLevel.Debug, Message = "Checking {Count} parameters for 'page' parameter")]
        public static partial void CheckingPageParameter(this ILogger logger, int count);

        [LoggerMessage(EventId = 5019, Level = LogLevel.Debug, Message = "Checking param: name={Name}, in={In}")]
        public static partial void CheckingParam(this ILogger logger, string name, string @in);

        [LoggerMessage(EventId = 5020, Level = LogLevel.Information, Message = "Found 'page' query parameter - endpoint supports pagination")]
        public static partial void FoundPageQueryParameter(this ILogger logger);

        [LoggerMessage(EventId = 5021, Level = LogLevel.Debug, Message = "No 'page' parameter found - endpoint does not support pagination")]
        public static partial void NoPageParameterFound(this ILogger logger);

        [LoggerMessage(EventId = 5022, Level = LogLevel.Debug, Message = "Found {Count} path-level parameters")]
        public static partial void FoundPathLevelParameters(this ILogger logger, int count);

        [LoggerMessage(EventId = 5023, Level = LogLevel.Debug, Message = "Path-level param: {Name}")]
        public static partial void PathLevelParam(this ILogger logger, string name);

        [LoggerMessage(EventId = 5024, Level = LogLevel.Debug, Message = "Found {Count} operation-level parameters")]
        public static partial void FoundOperationLevelParameters(this ILogger logger, int count);

        [LoggerMessage(EventId = 5025, Level = LogLevel.Debug, Message = "Operation-level param: {Name}")]
        public static partial void OperationLevelParam(this ILogger logger, string name);

        [LoggerMessage(EventId = 5026, Level = LogLevel.Debug, Message = "Total resolved parameters: {Count}")]
        public static partial void TotalResolvedParameters(this ILogger logger, int count);

        [LoggerMessage(EventId = 5027, Level = LogLevel.Warning, Message = "Could not validate response for {Url}")]
        public static partial void CouldNotValidateResponse(this ILogger logger, Exception exception, string url);

        [LoggerMessage(EventId = 5028, Level = LogLevel.Information, Message = "Processing HTTP response from {Url} (Status: {StatusCode}, ResponseSize: {Size} chars)")]
        public static partial void ProcessingHttpResponse(this ILogger logger, string url, int statusCode, int size);

        [LoggerMessage(EventId = 5029, Level = LogLevel.Debug, Message = "Response content length: {Length} chars")]
        public static partial void ResponseContentLength(this ILogger logger, int length);

        [LoggerMessage(EventId = 5030, Level = LogLevel.Information, Message = "✅ Successfully extracted and stored {Count} IDs from {Path} for root path '{RootPath}'")]
        public static partial void SuccessfullyExtractedIds(this ILogger logger, int count, string path, string rootPath);

        [LoggerMessage(EventId = 5031, Level = LogLevel.Debug, Message = "✅ Verified: {Count} IDs successfully stored in extractedIds dictionary for '{RootPath}'")]
        public static partial void VerifiedIdsStored(this ILogger logger, int count, string rootPath);

        [LoggerMessage(EventId = 5032, Level = LogLevel.Warning, Message = "⚠️ Warning: IDs extraction appeared successful but verification failed for '{RootPath}'")]
        public static partial void IdsVerificationFailed(this ILogger logger, string rootPath);

        [LoggerMessage(EventId = 5033, Level = LogLevel.Warning, Message = "No IDs could be extracted from response for path {Path} (root: {RootPath})")]
        public static partial void NoIdsExtracted(this ILogger logger, string path, string rootPath);

        [LoggerMessage(EventId = 5034, Level = LogLevel.Information, Message = "🔍 Looking for extracted IDs for root path '{RootPath}'. Available keys count: {Count}")]
        public static partial void LookingForExtractedIds(this ILogger logger, string rootPath, int count);

        [LoggerMessage(EventId = 5035, Level = LogLevel.Information, Message = "✅ Found {Count} extracted IDs for root path '{RootPath}'")]
        public static partial void FoundExtractedIds(this ILogger logger, int count, string rootPath);

        [LoggerMessage(EventId = 5036, Level = LogLevel.Information, Message = "🎯 Testing {Count} random IDs for endpoint {Path}")]
        public static partial void TestingRandomIds(this ILogger logger, int count, string path);

        [LoggerMessage(EventId = 5037, Level = LogLevel.Debug, Message = "Testing endpoint with extracted ID (path sanitized for security)")]
        public static partial void TestingEndpointWithExtractedId(this ILogger logger);

        [LoggerMessage(EventId = 5038, Level = LogLevel.Warning, Message = "⚠️ No extracted IDs available for root path '{RootPath}'. Dictionary contains {KeyCount} entries. Marking endpoint as NotTested: {Path}")]
        public static partial void NoExtractedIdsAvailable(this ILogger logger, string rootPath, int keyCount, string path);

        [LoggerMessage(EventId = 5039, Level = LogLevel.Debug, Message = "Available ID keys count in dictionary: {Count}")]
        public static partial void AvailableIdKeysCount(this ILogger logger, int count);

        [LoggerMessage(EventId = 5040, Level = LogLevel.Information, Message = "Starting ID extraction from JSON response for root path: {RootPath}")]
        public static partial void StartingIdExtraction(this ILogger logger, string rootPath);

        [LoggerMessage(EventId = 5041, Level = LogLevel.Debug, Message = "Found {Count} ID fields from OpenAPI schema")]
        public static partial void FoundIdFieldsFromSchema(this ILogger logger, int count);

        [LoggerMessage(EventId = 5042, Level = LogLevel.Debug, Message = "No ID fields identified from OpenAPI schema, falling back to common field names")]
        public static partial void FallingBackToCommonFieldNames(this ILogger logger);

        [LoggerMessage(EventId = 5043, Level = LogLevel.Debug, Message = "Parsed JSON type: {JsonType}")]
        public static partial void ParsedJsonType(this ILogger logger, string jsonType);

        [LoggerMessage(EventId = 5044, Level = LogLevel.Information, Message = "Found JSON array with {Count} items, extracting all IDs")]
        public static partial void FoundJsonArray(this ILogger logger, int count);

        [LoggerMessage(EventId = 5045, Level = LogLevel.Debug, Message = "Found ID in array item (ID hidden for security)")]
        public static partial void FoundIdInArrayItem(this ILogger logger);

        [LoggerMessage(EventId = 5046, Level = LogLevel.Debug, Message = "Found collection properties from OpenAPI schema: [{CollectionProps}]")]
        public static partial void FoundCollectionProperties(this ILogger logger, string collectionProps);

        [LoggerMessage(EventId = 5047, Level = LogLevel.Debug, Message = "Processing collection property '{PropName}' with {Count} items")]
        public static partial void ProcessingCollectionProperty(this ILogger logger, string propName, int count);

        [LoggerMessage(EventId = 5048, Level = LogLevel.Debug, Message = "Processing fallback collection property '{PropName}' with {Count} items")]
        public static partial void ProcessingFallbackCollectionProperty(this ILogger logger, string propName, int count);

        [LoggerMessage(EventId = 5049, Level = LogLevel.Warning, Message = "Failed to extract IDs from response for path {Path}")]
        public static partial void FailedToExtractIds(this ILogger logger, Exception exception, string path);

        [LoggerMessage(EventId = 5050, Level = LogLevel.Debug, Message = "Failed to extract ID fields from OpenAPI schema")]
        public static partial void FailedToExtractIdFieldsFromSchema(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 5051, Level = LogLevel.Debug, Message = "Failed to extract collection properties from OpenAPI schema")]
        public static partial void FailedToExtractCollectionProperties(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 5052, Level = LogLevel.Debug, Message = "Applied API Key authentication with header: {HeaderName}")]
        public static partial void AppliedApiKeyAuthenticationWithHeader(this ILogger logger, string headerName);

        [LoggerMessage(EventId = 5053, Level = LogLevel.Warning, Message = "Skipped API Key authentication due to invalid header name or value")]
        public static partial void SkippedApiKeyAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 5054, Level = LogLevel.Debug, Message = "Applied Bearer Token authentication")]
        public static partial void AppliedBearerTokenAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 5055, Level = LogLevel.Warning, Message = "Skipped Bearer Token authentication due to invalid token value")]
        public static partial void SkippedBearerTokenAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 5056, Level = LogLevel.Debug, Message = "Applied Basic authentication for user: {Username}")]
        public static partial void AppliedBasicAuthentication(this ILogger logger, string username);

        [LoggerMessage(EventId = 5057, Level = LogLevel.Debug, Message = "Applied custom header: {HeaderName}")]
        public static partial void AppliedCustomHeader(this ILogger logger, string headerName);

        [LoggerMessage(EventId = 5058, Level = LogLevel.Warning, Message = "Skipped invalid custom header")]
        public static partial void SkippedInvalidCustomHeader(this ILogger logger);

        [LoggerMessage(EventId = 5059, Level = LogLevel.Warning, Message = "User-supplied data source authentication was provided for a non-HTTPS endpoint. Skipping auth headers for {Url}")]
        public static partial void SkippedAuthForNonHttpsEndpoint(this ILogger logger, string url);

        [LoggerMessage(EventId = 5061, Level = LogLevel.Information, Message = "Compiled endpoint schema cache state at stage {Stage}. EntryCount: {EntryCount}, TotalKeyChars: {TotalKeyChars}")]
        public static partial void CompiledEndpointSchemaCacheState(this ILogger logger, string stage, int entryCount, long totalKeyChars);

        [LoggerMessage(EventId = 5062, Level = LogLevel.Information, Message = "Endpoint testing retention snapshot at stage {Stage}. ParsedJsonDocumentsInFlight: {ParsedJsonDocumentsInFlight}, RetainedResponseBodies: {RetainedResponseBodies}, RetainedResponseBodyChars: {RetainedResponseBodyChars}, ExtractedIdRoots: {ExtractedIdRoots}, ExtractedIdValues: {ExtractedIdValues}, ValidationSchemaCacheEntries: {ValidationSchemaCacheEntries}")]
        public static partial void EndpointTestingRetentionSnapshot(this ILogger logger, string stage, int parsedJsonDocumentsInFlight, int retainedResponseBodies, long retainedResponseBodyChars, int extractedIdRoots, int extractedIdValues, int validationSchemaCacheEntries);
    }
}
