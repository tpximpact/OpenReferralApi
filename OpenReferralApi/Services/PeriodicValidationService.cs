using Microsoft.Extensions.Options;
using OpenReferralApi.Models;

namespace OpenReferralApi.Services;

public class PeriodicValidationService : BackgroundService
{
    private readonly ILogger<PeriodicValidationService> _logger;
    private readonly IServiceScopeFactory _factory;
    private readonly TimeSpan _testingPeriod;
    private readonly bool _testingEnabled;

    public PeriodicValidationService(IServiceScopeFactory factory, ILogger<PeriodicValidationService> logger, 
        IOptions<ValidatorSettings> validatorSettings)
    {
        _factory = factory;
        _logger = logger;
        _testingPeriod = TimeSpan.FromHours(validatorSettings.Value.DashboardTestingPeriod);
        _testingEnabled = validatorSettings.Value.DashboardTestingEnabled;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_testingPeriod);

        _logger.LogInformation(_testingEnabled
            ? "Periodic dashboard testing enabled"
            : "Periodic dashboard testing disabled");

        while (!stoppingToken.IsCancellationRequested && _testingEnabled &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Periodic validation starting...");
                // Create scope, so we get request services
                await using var asyncScope = _factory.CreateAsyncScope();
                // Get service from scope
                var dashboardService = asyncScope.ServiceProvider.GetRequiredService<DashboardService>();

                var result = await dashboardService.ValidateDashboardServices();

                _logger.LogInformation(result.IsSuccess
                    ? "Periodic validation completed successfully"
                    : "Periodic validation completed unsuccessfully");
            }
            catch (Exception e)
            {
                _logger.LogError("Periodic validation failed with an error");
                _logger.LogError(e.Message);
            }
        }
    }
}