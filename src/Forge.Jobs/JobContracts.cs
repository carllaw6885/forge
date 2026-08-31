namespace Forge.Jobs;

/// <summary>
/// Context every durable job carries and restores (ADR 10): tenant and
/// correlation survive the queue, and an optional idempotency key deduplicates
/// enqueues of the same logical work.
/// </summary>
public sealed record JobContext(string? TenantId, string CorrelationId, string? IdempotencyKey = null);

/// <summary>A durable, at-least-once, idempotent unit of work (ADR 10). Resolved from DI per execution.</summary>
public interface IForgeJob
{
    Task ExecuteAsync(JobContext context, CancellationToken cancellationToken);
}

/// <summary>Provider-neutral scheduling contract; Quartz is the reference provider (ADR 10).</summary>
public interface IJobScheduler
{
    /// <summary>Enqueues one durable execution. A duplicate idempotency key is a no-op.</summary>
    Task EnqueueAsync<TJob>(JobContext context, CancellationToken cancellationToken) where TJob : IForgeJob;

    /// <summary>Registers a recurring job by cron expression.</summary>
    Task ScheduleRecurringAsync<TJob>(string cronExpression, JobContext context, CancellationToken cancellationToken)
        where TJob : IForgeJob;
}

/// <summary>A job that exhausted its retries; observable dead-letter projection (ADR 10).</summary>
public sealed record TerminalJobFailure(
    string JobType, string? TenantId, string CorrelationId, string Error, int Attempts, DateTimeOffset FailedAt);

/// <summary>Receives terminal failures for observation and audit.</summary>
public interface ITerminalFailureSink
{
    Task RecordAsync(TerminalJobFailure failure, CancellationToken cancellationToken);
}

/// <summary>In-memory reference sink; hosts replace it with an audited store.</summary>
public sealed class InMemoryTerminalFailureSink : ITerminalFailureSink
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<TerminalJobFailure> _failures = new();

    public IReadOnlyList<TerminalJobFailure> Failures => [.. _failures];

    public Task RecordAsync(TerminalJobFailure failure, CancellationToken cancellationToken)
    {
        _failures.Enqueue(failure);
        return Task.CompletedTask;
    }
}
