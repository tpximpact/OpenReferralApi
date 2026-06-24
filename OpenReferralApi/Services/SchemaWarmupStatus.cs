namespace OpenReferralApi.Services;

internal interface ISchemaWarmupStatusProvider
{
    SchemaWarmupStatusSnapshot GetSnapshot();
}

internal interface ISchemaWarmupStatusTracker : ISchemaWarmupStatusProvider
{
    void MarkSkipped(string reason);
    void MarkStarted(int configuredUrlCount);
    void MarkSuccess();
    void MarkFailure(string schemaUrl);
    void MarkCompleted(bool cancelled);
}

internal sealed class SchemaWarmupStatusSnapshot(
    string state,
    DateTimeOffset? lastStartedAtUtc,
    DateTimeOffset? lastCompletedAtUtc,
    int configuredUrlCount,
    int attemptedCount,
    int succeededCount,
    int failedCount,
    string? lastFailureUrl,
    string? skipReason)
{
    public string State { get; } = state;
    public DateTimeOffset? LastStartedAtUtc { get; } = lastStartedAtUtc;
    public DateTimeOffset? LastCompletedAtUtc { get; } = lastCompletedAtUtc;
    public int ConfiguredUrlCount { get; } = configuredUrlCount;
    public int AttemptedCount { get; } = attemptedCount;
    public int SucceededCount { get; } = succeededCount;
    public int FailedCount { get; } = failedCount;
    public string? LastFailureUrl { get; } = lastFailureUrl;
    public string? SkipReason { get; } = skipReason;
}

internal sealed class SchemaWarmupStatusTracker : ISchemaWarmupStatusTracker
{
    private readonly Lock _sync = new();
    private string _state = "not-started";
    private DateTimeOffset? _lastStartedAtUtc;
    private DateTimeOffset? _lastCompletedAtUtc;
    private int _configuredUrlCount;
    private int _attemptedCount;
    private int _succeededCount;
    private int _failedCount;
    private string? _lastFailureUrl;
    private string? _skipReason;

    public void MarkSkipped(string reason)
    {
        lock (_sync)
        {
            _state = "skipped";
            _skipReason = reason;
            _lastCompletedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkStarted(int configuredUrlCount)
    {
        lock (_sync)
        {
            _state = "running";
            _lastStartedAtUtc = DateTimeOffset.UtcNow;
            _configuredUrlCount = configuredUrlCount;
            _attemptedCount = 0;
            _succeededCount = 0;
            _failedCount = 0;
            _lastFailureUrl = null;
            _skipReason = null;
        }
    }

    public void MarkSuccess()
    {
        lock (_sync)
        {
            _attemptedCount++;
            _succeededCount++;
        }
    }

    public void MarkFailure(string schemaUrl)
    {
        lock (_sync)
        {
            _attemptedCount++;
            _failedCount++;
            _lastFailureUrl = schemaUrl;
        }
    }

    public void MarkCompleted(bool cancelled)
    {
        lock (_sync)
        {
            if (cancelled)
            {
                _state = "cancelled";
            }
            else if (_failedCount > 0)
            {
                _state = "completed-with-errors";
            }
            else
            {
                _state = "completed";
            }

            _lastCompletedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public SchemaWarmupStatusSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new SchemaWarmupStatusSnapshot(
                _state,
                _lastStartedAtUtc,
                _lastCompletedAtUtc,
                _configuredUrlCount,
                _attemptedCount,
                _succeededCount,
                _failedCount,
                _lastFailureUrl,
                _skipReason
            );
        }
    }
}
