using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.HealthChecks;

public class FeedValidationHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IFeedValidationService _feedValidationService;

    public FeedValidationHealthCheck(
        IConfiguration configuration,
        IFeedValidationService feedValidationService)
    {
        _configuration = configuration;
        _feedValidationService = feedValidationService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var enabled = _configuration.GetValue<bool>("FeedValidation:Enabled", false);
        if (!enabled)
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
