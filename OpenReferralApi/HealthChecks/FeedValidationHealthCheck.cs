using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.HealthChecks;

internal sealed class FeedValidationHealthCheck(
    IOptions<FeedValidationOptions> options,
    IFeedValidationService feedValidationService) : IHealthCheck
{
    private readonly FeedValidationOptions _options = options.Value;
    private readonly IFeedValidationService _feedValidationService = feedValidationService;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Feed validation is disabled"));
        }

        if (_feedValidationService is NullFeedValidationService)
        {
            return Task.FromResult(HealthCheckResult.Degraded("Feed validation service is not configured"));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Feed validation service is configured"));
    }
}
