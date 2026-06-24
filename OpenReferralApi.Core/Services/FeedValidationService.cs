using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;

namespace OpenReferralApi.Core.Services;

/// <summary>
/// Service for validating registered feeds and updating their status
/// </summary>
public interface IFeedValidationService
{
    Task<List<ServiceFeed>> GetAllFeedsAsync(CancellationToken cancellationToken = default);
    Task UpdateFeedStatusAsync(string feedId, bool isUp, bool isValid, string? error, double? responseTimeMs, int? validationErrorCount, CancellationToken cancellationToken = default);
    Task<FeedValidationResult> ValidateSingleFeedAsync(ServiceFeed feed, CancellationToken cancellationToken = default);
    Task<FeedValidationResult> ValidateAndUpdateFeedAsync(ServiceFeed feed, CancellationToken cancellationToken = default);
    Task<List<FeedValidationResult>> ValidateAndUpdateFeedsAsync(List<ServiceFeed> feeds, int maxConcurrency = 5, CancellationToken cancellationToken = default);
}

public class FeedValidationService : IFeedValidationService
{
    private readonly IMongoCollection<ServiceFeed> _servicesCollection;
    private readonly IOpenApiValidationService _validationService;
    private readonly ILogger<FeedValidationService> _logger;

    public FeedValidationService(
        IMongoClient mongoClient,
        IOptions<DatabaseOptions> databaseOptions,
        IOpenApiValidationService validationService,
        ILogger<FeedValidationService> logger)
    {
        var database = mongoClient.GetDatabase(databaseOptions.Value.DatabaseName);
        _servicesCollection = database.GetCollection<ServiceFeed>(databaseOptions.Value.ServicesCollection);
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<List<ServiceFeed>> GetAllFeedsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Filter for active feeds - check for boolean true, string "true", or nested value
            var filter = Builders<ServiceFeed>.Filter.Or(
                Builders<ServiceFeed>.Filter.Eq(f => f.ActiveField, true),
                Builders<ServiceFeed>.Filter.Eq(f => f.ActiveField, "true"),
                Builders<ServiceFeed>.Filter.Regex("active.value", new BsonRegularExpression("^true$", "i"))
            );

            return await _servicesCollection
                .Find(filter)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.FailedToRetrieveFeeds(ex);
            return [];
        }
    }

    public async Task UpdateFeedStatusAsync(
        string feedId,
        bool isUp,
        bool isValid,
        string? error,
        double? responseTimeMs,
        int? validationErrorCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<ServiceFeed>.Filter.Eq(f => f.Id, feedId);

            // Read the current document to check structure of existing status fields
            var currentFeed = await _servicesCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (currentFeed == null)
            {
                _logger.FeedNotFoundForUpdate(feedId);
                return;
            }

            var updateBuilder = Builders<ServiceFeed>.Update;
            var updates = new List<UpdateDefinition<ServiceFeed>>();

            // For status fields, if they exist as BsonDocuments, update nested value field
            // otherwise set as simple boolean
            if (currentFeed.StatusIsUp?.IsBsonDocument ?? false)
            {
                updates.Add(updateBuilder.Set("statusIsUp.value", isUp));
            }
            else
            {
                updates.Add(updateBuilder.Set(f => f.StatusIsUp, isUp));
            }

            if (currentFeed.StatusIsValid?.IsBsonDocument ?? false)
            {
                updates.Add(updateBuilder.Set("statusIsValid.value", isValid));
            }
            else
            {
                updates.Add(updateBuilder.Set(f => f.StatusIsValid, isValid));
            }

            if (currentFeed.StatusOverall?.IsBsonDocument ?? false)
            {
                updates.Add(updateBuilder.Set("statusOverall.value", isValid));
            }
            else
            {
                updates.Add(updateBuilder.Set(f => f.StatusOverall, isValid));
            }

            updates.Add(updateBuilder.Set(f => f.LastChecked, DateTime.UtcNow));
            updates.Add(updateBuilder.Set(f => f.LastError, error));
            updates.Add(updateBuilder.Set(f => f.ResponseTimeMs, responseTimeMs));
            updates.Add(updateBuilder.Set(f => f.ValidationErrorCount, validationErrorCount));

            // Update lastTested with current timestamp and results URL
            var lastTestedDoc = new BsonDocument
      {
        { "value", DateTime.UtcNow },
        { "url", $"/developers/dashboard/{feedId}" }
      };
            updates.Add(updateBuilder.Set(f => f.LastTested, lastTestedDoc));

            var combinedUpdate = updateBuilder.Combine(updates);
            _ = await _servicesCollection.UpdateOneAsync(filter, combinedUpdate, cancellationToken: cancellationToken);

            _logger.FeedStatusUpdated(feedId, isUp, isValid, responseTimeMs, validationErrorCount);
        }
        catch (Exception ex)
        {
            _logger.FailedToUpdateFeedStatus(ex, feedId);
        }
    }

    public async Task<FeedValidationResult> ValidateSingleFeedAsync(
        ServiceFeed feed,
        CancellationToken cancellationToken = default)
    {
        var result = new FeedValidationResult
        {
            FeedId = feed.Id!,
            FeedUrl = feed.Url,
            FeedName = feed.NameAsString
        };

        try
        {
            _logger.ValidatingFeed(feed.NameAsString ?? "Unnamed", feed.Url);

            var validationRequest = new OpenApiValidationRequest
            {
                BaseUrl = feed.Url,
                Options = new OpenApiValidationOptions
                {
                    TimeoutSeconds = 60,
                    MaxConcurrentRequests = 10,
                    ReportAdditionalFields = false
                }
            };

            var validationResult = await _validationService.ValidateOpenApiSpecificationAsync(
                validationRequest,
                cancellationToken);

            var isUp = false;
            var validationErrorCount = 0;

            foreach (var endpointTest in validationResult.EndpointTests)
            {
                if (!isUp && endpointTest.TestResults != null)
                {
                    foreach (var tr in endpointTest.TestResults)
                    {
                        if (tr.IsSuccessStatusCode)
                        {
                            isUp = true;
                            break;
                        }
                    }
                }

                if (endpointTest.ValidationErrors != null)
                {
                    validationErrorCount += endpointTest.ValidationErrors.Count;
                }
            }

            result.IsUp = isUp;
            result.IsValid = validationResult.IsValid;
            result.ResponseTimeMs = validationResult.Duration.TotalMilliseconds;
            result.ValidationErrorCount = validationErrorCount;

            if (!validationResult.IsValid)
            {
                var formattedErrorsCount = 0;
                var sb = new System.Text.StringBuilder();

                foreach (var endpointTest in validationResult.EndpointTests)
                {
                    if (endpointTest.ValidationErrors == null) continue;

                    foreach (var error in endpointTest.ValidationErrors)
                    {
                        if (formattedErrorsCount > 0)
                        {
                            sb.Append("; ");
                        }
                        sb.Append(error.Path).Append(": ").Append(error.Message);
                        formattedErrorsCount++;

                        if (formattedErrorsCount >= 5) break;
                    }

                    if (formattedErrorsCount >= 5) break;
                }

                result.ErrorMessage = formattedErrorsCount > 0
                    ? sb.ToString()
                    : "Validation failed with no specific errors";
            }

            _logger.FeedValidationCompleted(feed.NameAsString ?? "Unnamed", result.IsUp, result.IsValid, result.ValidationErrorCount);
        }
        catch (HttpRequestException ex)
        {
            _logger.FeedNotAccessible(ex, feed.Url);
            result.IsUp = false;
            result.IsValid = false;
            result.ErrorMessage = $"HTTP error: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}";
        }
        catch (TaskCanceledException ex)
        {
            _logger.FeedValidationTimedOut(ex, feed.Url);
            result.IsUp = false;
            result.IsValid = false;
            result.ErrorMessage = "Request timed out";
        }
        catch (Exception ex)
        {
            _logger.UnexpectedErrorValidatingFeed(ex, feed.Url);
            result.IsUp = false;
            result.IsValid = false;
            result.ErrorMessage = $"Unexpected error: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}";
        }

        return result;
    }

    public async Task<List<FeedValidationResult>> ValidateAndUpdateFeedsAsync(
        List<ServiceFeed> feeds,
        int maxConcurrency = 5,
        CancellationToken cancellationToken = default)
    {
        if (feeds.Count == 0)
        {
            return [];
        }

        var results = new FeedValidationResult[feeds.Count];

        await Parallel.ForEachAsync(Enumerable.Range(0, feeds.Count), new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        }, async (i, ct) =>
        {
            var feed = feeds[i];
            try
            {
                results[i] = await ValidateAndUpdateFeedAsync(feed, ct);
            }
            catch (Exception ex)
            {
                _logger.FailedToValidateFeed(ex, feed.Id ?? string.Empty);
                results[i] = new FeedValidationResult
                {
                    FeedId = feed.Id ?? string.Empty,
                    FeedUrl = feed.Url,
                    FeedName = feed.NameAsString,
                    IsUp = false,
                    IsValid = false,
                    ErrorMessage = $"Validation error: {TextSanitizer.SanitizeExceptionMessage(ex.Message)}"
                };
            }
        });

        return [.. results];
    }

    public async Task<FeedValidationResult> ValidateAndUpdateFeedAsync(
        ServiceFeed feed,
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateSingleFeedAsync(feed, cancellationToken);

        await UpdateFeedStatusAsync(
            feedId: result.FeedId,
            isUp: result.IsUp,
            isValid: result.IsValid,
            error: result.ErrorMessage,
            responseTimeMs: result.ResponseTimeMs,
            validationErrorCount: result.ValidationErrorCount,
            cancellationToken: cancellationToken);

        return result;
    }

}

/// <summary>
/// Null implementation when MongoDB is not configured
/// </summary>
public class NullFeedValidationService(ILogger<NullFeedValidationService> logger) : IFeedValidationService
{
    private readonly ILogger<NullFeedValidationService> _logger = logger;


    public Task<List<ServiceFeed>> GetAllFeedsAsync(CancellationToken cancellationToken = default)
    {
        _logger.FeedValidationServiceNotAvailable();
        return Task.FromResult(new List<ServiceFeed>());
    }

    public Task UpdateFeedStatusAsync(string feedId, bool isUp, bool isValid, string? error, double? responseTimeMs, int? validationErrorCount, CancellationToken cancellationToken = default)
    {
        _logger.FeedValidationServiceNotAvailable();
        return Task.CompletedTask;
    }

    public Task<FeedValidationResult> ValidateSingleFeedAsync(ServiceFeed feed, CancellationToken cancellationToken = default)
    {
        _logger.FeedValidationServiceNotAvailable();
        return Task.FromResult(new FeedValidationResult
        {
            FeedId = feed.Id ?? string.Empty,
            FeedUrl = feed.Url,
            FeedName = feed.NameAsString,
            IsUp = false,
            IsValid = false,
            ErrorMessage = "Feed validation service is not available. MongoDB is not configured."
        });
    }

    public Task<FeedValidationResult> ValidateAndUpdateFeedAsync(ServiceFeed feed, CancellationToken cancellationToken = default)
    {
        _logger.FeedValidationServiceNotAvailable();
        return Task.FromResult(new FeedValidationResult
        {
            FeedId = feed.Id ?? string.Empty,
            FeedUrl = feed.Url,
            FeedName = feed.NameAsString,
            IsUp = false,
            IsValid = false,
            ErrorMessage = "Feed validation service is not available. MongoDB is not configured."
        });
    }

    public Task<List<FeedValidationResult>> ValidateAndUpdateFeedsAsync(List<ServiceFeed> feeds, int maxConcurrency = 5, CancellationToken cancellationToken = default)
    {
        _logger.FeedValidationServiceNotAvailable();
        return Task.FromResult(new List<FeedValidationResult>());
    }
}

/// <summary>
/// Result of validating a single feed
/// </summary>
public class FeedValidationResult
{
    public string FeedId { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string? FeedName { get; set; }
    public bool IsUp { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public double? ResponseTimeMs { get; set; }
    public int ValidationErrorCount { get; set; }
}
