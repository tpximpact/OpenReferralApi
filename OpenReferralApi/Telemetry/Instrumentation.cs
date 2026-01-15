using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenReferralApi.Telemetry;

/// <summary>
/// Provides telemetry instrumentation for the Open Referral UK API
/// </summary>
public static class Instrumentation
{
    public const string ServiceName = "OpenReferralUK.API";
    public const string ServiceVersion = "1.0.0";

    // Activity source for distributed tracing
    public static ActivitySource ActivitySource { get; } = new(ServiceName, ServiceVersion);

    // Metrics
    private static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Counters
    public static Counter<long> ValidationRequestsCounter { get; } = 
        Meter.CreateCounter<long>(
            "openreferral.validations.requests",
            description: "Total number of validation requests");

    public static Counter<long> ValidationSuccessCounter { get; } = 
        Meter.CreateCounter<long>(
            "openreferral.validations.success",
            description: "Number of successful validations");

    public static Counter<long> ValidationFailureCounter { get; } = 
        Meter.CreateCounter<long>(
            "openreferral.validations.failure",
            description: "Number of failed validations");

    // Histograms
    public static Histogram<double> ValidationDuration { get; } = 
        Meter.CreateHistogram<double>(
            "openreferral.validations.duration",
            unit: "ms",
            description: "Duration of validation requests in milliseconds");

    public static Histogram<double> EndpointTestDuration { get; } = 
        Meter.CreateHistogram<double>(
            "openreferral.endpoint_tests.duration",
            unit: "ms",
            description: "Duration of endpoint tests in milliseconds");

    // ObservableGauges can be added as needed
    public static ObservableGauge<int> ActiveValidations { get; } = 
        Meter.CreateObservableGauge<int>(
            "openreferral.validations.active",
            () => GetActiveValidationsCount(),
            description: "Number of validations currently in progress");

    private static int _activeValidations = 0;

    public static void IncrementActiveValidations() => 
        Interlocked.Increment(ref _activeValidations);

    public static void DecrementActiveValidations() => 
        Interlocked.Decrement(ref _activeValidations);

    private static int GetActiveValidationsCount() => _activeValidations;
}
