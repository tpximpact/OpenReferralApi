using Microsoft.Extensions.Options;

namespace OpenReferralApi.Services;

/// <summary>
/// Warms frequently-used remote schemas into cache after startup.
/// </summary>
internal sealed class SchemaWarmupBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<SpecificationOptions> options,
    IOptions<CacheOptions> cacheOptions,
    ISchemaWarmupStatusTracker statusTracker,
    ILogger<SchemaWarmupBackgroundService> logger,
    ISchemaWarmupExecutor? executor = null) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<SchemaWarmupBackgroundService> _logger = logger;
    private readonly SpecificationOptions _options = options.Value ?? new SpecificationOptions();
    private readonly CacheOptions _cacheOptions = cacheOptions.Value ?? new CacheOptions();
    private readonly ISchemaWarmupStatusTracker _statusTracker = statusTracker;
    private readonly ISchemaWarmupExecutor _executor = executor ?? new SchemaWarmupExecutor();

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _executor.WarmupAsync(_options, _cacheOptions, _statusTracker, _serviceProvider, _logger, stoppingToken);
}

