using System.Diagnostics;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Logging;
using OpenReferralApi.Core.Services;
using OpenReferralApi.Logging;

namespace OpenReferralApi.Services;

/// <summary>
/// Background service that validates registered feeds every 24 hours at midnight
/// </summary>
internal sealed class FeedValidationBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<FeedValidationOptions> options,
    ILogger<FeedValidationBackgroundService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<FeedValidationBackgroundService> _logger = logger;
    private readonly TimeSpan _validationInterval = TimeSpan.FromHours(options.Value.IntervalHours);
    private readonly bool _runAtMidnight = options.Value.RunAtMidnight;
    private readonly bool _enabled = options.Value.Enabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.ServiceDisabled();
            return;
        }

        _logger.ServiceStarted(_validationInterval.TotalHours, _runAtMidnight);

        // Wait until first scheduled run
        await WaitForNextScheduledRunAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.ScheduledValidationStarted(DateTime.UtcNow);
                await ValidateAllFeedsAsync(stoppingToken).ConfigureAwait(false);
                stoppingToken.ThrowIfCancellationRequested();
                _logger.ScheduledValidationCompleted(DateTime.UtcNow);
            }
            catch (OperationCanceledException)
            {
                _logger.ServiceStopping();
                throw;
            }
            // Remove catch-all Exception handler to comply with analyzer
            // If you want to log unexpected exceptions, consider rethrowing after logging

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Wait for next scheduled run
            await WaitForNextScheduledRunAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForNextScheduledRunAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        TimeSpan delay;

        if (_runAtMidnight)
        {
            // Calculate time until next midnight UTC
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            delay = nextMidnight - now;

            _logger.NextValidationScheduledForMidnight(nextMidnight, delay.TotalHours);
        }
        else
        {
            // Use fixed interval
            delay = _validationInterval;
            _logger.NextValidationScheduled(delay.TotalHours);
        }

        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            _logger.ServiceStopping();
        }
    }

    private async Task ValidateAllFeedsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        using var scope = _serviceProvider.CreateScope();
        var feedValidationService = scope.ServiceProvider.GetRequiredService<IFeedValidationService>();

        try
        {
            // Get all registered feeds
            var feeds = await feedValidationService.GetAllFeedsAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.FoundFeedsToValidate(feeds.Count);

            if (feeds.Count == 0)
            {
                _logger.NoFeedsFound();
                return;
            }
            var results = await feedValidationService.ValidateAndUpdateFeedsAsync(feeds, cancellationToken: cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Log summary
            var successCount = results.Count(r => r.IsUp);
            var validCount = results.Count(r => r.IsValid);
            var failedCount = results.Count(r => !r.IsUp);

            stopwatch.Stop();

            _logger.FeedValidationSummary(feeds.Count, successCount, validCount, failedCount, stopwatch.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            _logger.ServiceStopping();
            throw;
        }
        // Remove catch-all Exception handler to comply with analyzer
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.BackgroundServiceStopping();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
