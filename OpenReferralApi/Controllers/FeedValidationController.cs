using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenReferralApi.Core.Services;
using OpenReferralApi.Logging;
using OpenReferralApi.Models;

namespace OpenReferralApi.Controllers;

/// <summary>
/// Controller for managing and testing feed validation
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/[controller]")]
[EnableRateLimiting("fixed")]
internal sealed class FeedValidationController(
    IFeedValidationService feedValidationService,
    ILogger<FeedValidationController> logger) : ControllerBase
{
    private readonly IFeedValidationService _feedValidationService = feedValidationService;
    private readonly ILogger<FeedValidationController> _logger = logger;

    /// <summary>
    /// Get all registered feeds with their current status
    /// </summary>
    /// <returns>List of all feeds</returns>
    [HttpGet("feeds")]
    [ProducesResponseType(typeof(List<ServiceFeed>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ServiceFeed>>> GetAllFeeds(CancellationToken cancellationToken)
    {
        var feeds = await _feedValidationService.GetAllFeedsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(feeds);
    }

    /// <summary>
    /// Manually trigger validation for all feeds
    /// </summary>
    /// <returns>Validation results for all feeds</returns>
    [HttpPost("validate-all")]
    [ProducesResponseType(typeof(FeedValidationSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FeedValidationSummary>> ValidateAllFeeds(CancellationToken cancellationToken)
    {
        _logger.ManualValidationTriggeredForAllFeeds();

        var feeds = await _feedValidationService.GetAllFeedsAsync(cancellationToken).ConfigureAwait(false);

        if (feeds.Count == 0)
        {
            return Ok(new FeedValidationSummary
            {
                TotalFeeds = 0,
                Message = "No feeds found in database"
            });
        }
        var results = await _feedValidationService.ValidateAndUpdateFeedsAsync(feeds, cancellationToken: cancellationToken).ConfigureAwait(false);

        var upFeeds = 0;
        var validFeeds = 0;
        var downFeeds = 0;
        var invalidFeeds = 0;
        var responseTimeSum = 0.0;
        var responseTimeCount = 0;

        foreach (var r in results)
        {
            if (r.IsUp)
            {
                upFeeds++;
                if (r.IsValid)
                {
                    validFeeds++;
                }
                else
                {
                    invalidFeeds++;
                }
            }
            else
            {
                downFeeds++;
            }

            if (r.ResponseTimeMs.HasValue)
            {
                responseTimeSum += r.ResponseTimeMs.Value;
                responseTimeCount++;
            }
        }

        var summary = new FeedValidationSummary
        {
            TotalFeeds = feeds.Count,
            UpFeeds = upFeeds,
            ValidFeeds = validFeeds,
            DownFeeds = downFeeds,
            InvalidFeeds = invalidFeeds,
            AverageResponseTimeMs = responseTimeCount > 0 ? responseTimeSum / responseTimeCount : null,
            Results = results
        };

        _logger.ManualValidationCompleted(summary.TotalFeeds, summary.UpFeeds, summary.ValidFeeds);

        return Ok(summary);
    }

    /// <summary>
    /// Manually trigger validation for a specific feed by ID
    /// </summary>
    /// <param name="feedId">The MongoDB ObjectId of the feed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result for the specified feed</returns>
    [HttpPost("validate/{feedId}")]
    [ProducesResponseType(typeof(FeedValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FeedValidationResult>> ValidateFeed(
        string feedId,
        CancellationToken cancellationToken)
    {
        var feed = await _feedValidationService.GetFeedByIdAsync(feedId, cancellationToken).ConfigureAwait(false);

        if (feed == null || !feed.IsActive)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = "Feed not found",
                FeedId = feedId
            });
        }

        var safeFeedId = feedId?.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        _logger.ManualValidationTriggeredForFeed(safeFeedId ?? string.Empty);

        var result = await _feedValidationService.ValidateAndUpdateFeedAsync(feed, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}

/// <summary>
/// Summary of feed validation results
/// </summary>
internal sealed class FeedValidationSummary
{
    public int TotalFeeds { get; set; }
    public int UpFeeds { get; set; }
    public int ValidFeeds { get; set; }
    public int DownFeeds { get; set; }
    public int InvalidFeeds { get; set; }
    public double? AverageResponseTimeMs { get; set; }
    public string? Message { get; set; }
    public List<FeedValidationResult> Results { get; set; } = [];
}
