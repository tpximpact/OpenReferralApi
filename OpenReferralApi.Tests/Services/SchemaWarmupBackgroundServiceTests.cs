using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenReferralApi.Core.Services;
using OpenReferralApi.Services;
using System.Reflection;

namespace OpenReferralApi.Tests.Services;

[TestFixture]
public class SchemaWarmupBackgroundServiceTests
{
    [Test]
    public async Task ExecuteAsync_WhenDisabled_SetsSkippedStatus()
    {
        var resolverMock = new Mock<ISchemaResolverService>();
        using var serviceProvider = BuildServiceProvider(resolverMock.Object);
        var statusTracker = new SchemaWarmupStatusTracker();
        var logger = new Mock<ILogger<SchemaWarmupBackgroundService>>();

        var service = CreateService(
            serviceProvider,
            Options.Create(new SpecificationOptions { WarmupEnabled = false }),
            Options.Create(new CacheOptions { Enabled = true }),
            statusTracker,
            logger.Object);

        await RunOnce(service, CancellationToken.None);

        var snapshot = statusTracker.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.State, Is.EqualTo("skipped"));
            Assert.That(snapshot.SkipReason, Is.EqualTo("disabled"));
        }
        resolverMock.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenCacheDisabled_SetsSkippedStatus()
    {
        var resolverMock = new Mock<ISchemaResolverService>();
        using var serviceProvider = BuildServiceProvider(resolverMock.Object);
        var statusTracker = new SchemaWarmupStatusTracker();
        var logger = new Mock<ILogger<SchemaWarmupBackgroundService>>();

        var service = CreateService(
            serviceProvider,
            Options.Create(new SpecificationOptions
            {
                WarmupEnabled = true,
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-TEST"] = "https://example.com/schema.json"
                }
            }),
            Options.Create(new CacheOptions { Enabled = false }),
            statusTracker,
            logger.Object);

        await RunOnce(service, CancellationToken.None);

        var snapshot = statusTracker.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.State, Is.EqualTo("skipped"));
            Assert.That(snapshot.SkipReason, Is.EqualTo("cache-disabled"));
        }
        resolverMock.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithMixedResults_TracksCountsAndFailureState()
    {
        var successUrl = "https://example.com/schema-success.json";
        var failUrl = "https://example.com/schema-fail.json";

        var resolverMock = new Mock<ISchemaResolverService>();
        resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .Returns<string, string?, DataSourceAuthentication?>((_, baseUri, _) =>
            {
                if (string.Equals(baseUri, failUrl, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("warmup failure");
                }

                return Task.FromResult("{}");
            });

        using var serviceProvider = BuildServiceProvider(resolverMock.Object);
        var statusTracker = new SchemaWarmupStatusTracker();
        var logger = new Mock<ILogger<SchemaWarmupBackgroundService>>();

        var service = CreateService(
            serviceProvider,
            Options.Create(new SpecificationOptions
            {
                WarmupEnabled = true,
                WarmupStartupDelaySeconds = 0,
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-SUCCESS"] = successUrl,
                    ["HSDS-UK-FAIL"] = failUrl
                }
            }),
            Options.Create(new CacheOptions { Enabled = true }),
            statusTracker,
            logger.Object);

        await RunOnce(service, CancellationToken.None);

        var snapshot = statusTracker.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.State, Is.EqualTo("completed-with-errors"));
            Assert.That(snapshot.ConfiguredUrlCount, Is.EqualTo(2));
            Assert.That(snapshot.AttemptedCount, Is.EqualTo(2));
            Assert.That(snapshot.SucceededCount, Is.EqualTo(1));
            Assert.That(snapshot.FailedCount, Is.EqualTo(1));
            Assert.That(snapshot.LastFailureUrl, Is.EqualTo(failUrl));
        }
    }

    [Test]
    public async Task ExecuteAsync_WithDuplicateUrls_DeduplicatesRequests()
    {
        var resolverMock = new Mock<ISchemaResolverService>();
        resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync("{}");

        using var serviceProvider = BuildServiceProvider(resolverMock.Object);
        var statusTracker = new SchemaWarmupStatusTracker();
        var logger = new Mock<ILogger<SchemaWarmupBackgroundService>>();

        var canonicalUrl = "https://example.com/schema.json";

        var service = CreateService(
            serviceProvider,
            Options.Create(new SpecificationOptions
            {
                WarmupEnabled = true,
                WarmupStartupDelaySeconds = 0,
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-PRIMARY"] = canonicalUrl,
                    ["HSDS-UK-TRIMMED"] = $" {canonicalUrl} ",
                    ["HSDS-UK-UPPER"] = "HTTPS://EXAMPLE.COM/SCHEMA.JSON"
                }
            }),
            Options.Create(new CacheOptions { Enabled = true }),
            statusTracker,
            logger.Object);

        await RunOnce(service, CancellationToken.None);

        var snapshot = statusTracker.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.State, Is.EqualTo("completed"));
            Assert.That(snapshot.ConfiguredUrlCount, Is.EqualTo(1));
            Assert.That(snapshot.AttemptedCount, Is.EqualTo(1));
            Assert.That(snapshot.SucceededCount, Is.EqualTo(1));
        }

        resolverMock.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), null), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenCancelledBeforeLoop_MarksCancelled()
    {
        var resolverMock = new Mock<ISchemaResolverService>();
        resolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync("{}");

        using var serviceProvider = BuildServiceProvider(resolverMock.Object);
        var statusTracker = new SchemaWarmupStatusTracker();
        var logger = new Mock<ILogger<SchemaWarmupBackgroundService>>();

        var service = CreateService(
            serviceProvider,
            Options.Create(new SpecificationOptions
            {
                WarmupEnabled = true,
                WarmupStartupDelaySeconds = 0,
                Urls = new Dictionary<string, string>
                {
                    ["HSDS-UK-TEST"] = "https://example.com/schema.json"
                }
            }),
            Options.Create(new CacheOptions { Enabled = true }),
            statusTracker,
            logger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await RunOnce(service, cts.Token);

        var snapshot = statusTracker.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.State, Is.EqualTo("cancelled"));
            Assert.That(snapshot.ConfiguredUrlCount, Is.EqualTo(1));
            Assert.That(snapshot.AttemptedCount, Is.Zero);
        }

        resolverMock.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
    }

    private static ServiceProvider BuildServiceProvider(ISchemaResolverService resolver)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => resolver);
        return services.BuildServiceProvider();
    }

    private static SchemaWarmupBackgroundService CreateService(
        IServiceProvider serviceProvider,
        IOptions<SpecificationOptions> options,
        IOptions<CacheOptions> cacheOptions,
        ISchemaWarmupStatusTracker statusTracker,
        ILogger<SchemaWarmupBackgroundService> logger,
        ISchemaWarmupExecutor? executor = null)
    {
        return new SchemaWarmupBackgroundService(serviceProvider, options, cacheOptions, statusTracker, logger, executor);
    }

    private static Task RunOnce(SchemaWarmupBackgroundService service, CancellationToken cancellationToken)
    {
        var method = typeof(SchemaWarmupBackgroundService).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, "ExecuteAsync method not found via reflection.");

        var result = method!.Invoke(service, [cancellationToken]) as Task;
        Assert.That(result, Is.Not.Null, "ExecuteAsync did not return a Task.");

        return result!;
    }
}
