using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenReferralApi.Models.Settings;

namespace OpenReferralApi.Services;

public class PeriodicValidationService : BackgroundService, IHealthCheck
{
    private readonly ILogger<PeriodicValidationService> _logger;
    private readonly IServiceScopeFactory _factory;
    private readonly TimeSpan _testingPeriod;
    private readonly bool _testingEnabled;
    private DateTime? _lastExecutionTime;
    private bool _isHealthy = true;
    private string _healthMessage = "Service not yet started";

    public PeriodicValidationService(IServiceScopeFactory factory, ILogger<PeriodicValidationService> logger, 
        IOptions<ValidatorSettings> validatorSettings)
    {
        _factory = factory;
        _logger = logger;
        _testingPeriod = TimeSpan.FromHours(validatorSettings.Value.DashboardTestingPeriod);
        _testingEnabled = validatorSettings.Value.DashboardTestingEnabled;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_testingEnabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Background validation is disabled"));
        }

        var data = new Dictionary<string, object>
        {
            { "enabled", _testingEnabled },
            { "testingPeriod", _testingPeriod.TotalHours + " hours" },
            { "lastExecutionTime", _lastExecutionTime?.ToString("o") ?? "Never" }
        };

        if (!_isHealthy)
        {
            return Task.FromResult(HealthCheckResult.Degraded(_healthMessage, data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(_healthMessage, data));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_testingEnabled)
        {
            _logger.LogInformation("Periodic dashboard testing disabled");
            return;
        }
        
        using var timer = new PeriodicTimer(_testingPeriod);

        _logger.LogInformation("Periodic dashboard testing enabled");
        await ExecuteTests();

        while (!stoppingToken.IsCancellationRequested && _testingEnabled && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ExecuteTests();
        }
    }

    private async Task ExecuteTests()
    {
        try
        {
            _logger.LogInformation("Periodic validation starting...");
            _lastExecutionTime = DateTime.UtcNow;
            
            // Create scope, so we get request services
            await using var asyncScope = _factory.CreateAsyncScope();
            // Get service from scope
            var dashboardService = asyncScope.ServiceProvider.GetRequiredService<DashboardService>();

            var result = await dashboardService.ValidateDashboardServices();

            _isHealthy = result.IsSuccess;
            _healthMessage = result.IsSuccess
                ? "Periodic validation completed successfully"
                : "Periodic validation completed with errors";
            
            _logger.LogInformation(_healthMessage);
        }
        catch (Exception e)
        {
            _isHealthy = false;
            _healthMessage = $"Periodic validation failed: {e.Message}";
            _logger.LogError("Periodic validation failed with an error");
            _logger.LogError(e.Message);
        }
    }
}