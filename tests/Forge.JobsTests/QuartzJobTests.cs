using System.Collections.Concurrent;
using Forge.Auditing;
using Forge.Core.Validation;
using Forge.Jobs;
using Forge.Jobs.Quartz;
using Forge.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Forge.JobsTests;

public sealed class RecordingJob(ConcurrentQueue<JobContext> log) : IForgeJob
{
    public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        log.Enqueue(context);
        return Task.CompletedTask;
    }
}

public sealed class TenantObservingJob(ConcurrentQueue<string?> log, ICurrentTenant tenant) : IForgeJob
{
    public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        log.Enqueue(tenant.Id);
        return Task.CompletedTask;
    }
}

public sealed class FlakyJob(ConcurrentQueue<int> attempts) : IForgeJob
{
    public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        attempts.Enqueue(attempts.Count + 1);
        return attempts.Count < 2
            ? throw new InvalidOperationException("transient")
            : Task.CompletedTask;
    }
}

public sealed class AlwaysFailingJob : IForgeJob
{
    public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("permanent");
}

/// <summary>
/// One provider for the whole class: Quartz bridges logging through static
/// state, so per-test containers would observe each other's disposal.
/// </summary>
public sealed class QuartzTestFixture : IAsyncLifetime
{
    public ServiceProvider Provider { get; private set; } = null!;
    public ConcurrentQueue<JobContext> RecordedContexts { get; } = new();
    public ConcurrentQueue<string?> ObservedTenants { get; } = new();
    public ConcurrentQueue<int> FlakyAttempts { get; } = new();

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        // NullLoggerFactory is static and dispose-proof: Quartz's static log
        // bridge captures the first factory it sees, so a disposable one from
        // another fixture would poison every later scheduler in the process.
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Logger<>));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CurrentTenant>();
        services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddSingleton<IAuditRedactionPolicy, DefaultAuditRedactionPolicy>();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();
        services.AddSingleton(RecordedContexts);
        services.AddSingleton(ObservedTenants);
        services.AddSingleton(FlakyAttempts);
        services.AddScoped<RecordingJob>();
        services.AddScoped<TenantObservingJob>();
        services.AddScoped<FlakyJob>();
        services.AddScoped<AlwaysFailingJob>();
        services.AddForgeQuartzJobs(new ForgeQuartzOptions
        {
            UseInMemoryStore = true,
            SchedulerName = $"test-{Guid.NewGuid():N}",
        });

        Provider = services.BuildServiceProvider();
        foreach (var hosted in Provider.GetServices<IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync() => await Provider.DisposeAsync();
}

public class QuartzJobTests(QuartzTestFixture fx) : IClassFixture<QuartzTestFixture>
{
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct, int timeoutMs = 20000)
    {
        for (var waited = 0; !condition() && waited < timeoutMs; waited += 100)
        {
            await Task.Delay(100, ct);
        }

        Assert.True(condition(), "condition not met within timeout");
    }

    [Fact]
    public async Task Job_executes_with_restored_tenant_and_correlation()
    {
        var ct = TestContext.Current.CancellationToken;
        var scheduler = fx.Provider.GetRequiredService<IJobScheduler>();

        await scheduler.EnqueueAsync<RecordingJob>(new JobContext("tenant-a", "corr-1"), ct);
        await scheduler.EnqueueAsync<TenantObservingJob>(new JobContext("tenant-b", "corr-2"), ct);

        await WaitUntilAsync(
            () => fx.RecordedContexts.Any(c => c.CorrelationId == "corr-1") && !fx.ObservedTenants.IsEmpty, ct);
        var context = Assert.Single(fx.RecordedContexts, c => c.CorrelationId == "corr-1");
        Assert.Equal("tenant-a", context.TenantId);
        Assert.Contains("tenant-b", fx.ObservedTenants); // ambient tenant restored inside the job
    }

    [Fact]
    public async Task Duplicate_idempotency_key_enqueues_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var scheduler = fx.Provider.GetRequiredService<IJobScheduler>();

        var context = new JobContext("tenant-a", "corr-dup", IdempotencyKey: "job-42");
        await scheduler.EnqueueAsync<RecordingJob>(context, ct);
        await scheduler.EnqueueAsync<RecordingJob>(context, ct);

        await WaitUntilAsync(() => fx.RecordedContexts.Any(c => c.CorrelationId == "corr-dup"), ct);
        await Task.Delay(500, ct); // give a duplicate a chance to (incorrectly) run
        Assert.Single(fx.RecordedContexts, c => c.CorrelationId == "corr-dup");
    }

    [Fact]
    public async Task Transient_failure_retries_and_succeeds_without_terminal_projection()
    {
        var ct = TestContext.Current.CancellationToken;

        await fx.Provider.GetRequiredService<IJobScheduler>()
            .EnqueueAsync<FlakyJob>(new JobContext(null, "corr-retry"), ct);

        await WaitUntilAsync(() => fx.FlakyAttempts.Count >= 2, ct);
        var sink = (InMemoryTerminalFailureSink)fx.Provider.GetRequiredService<ITerminalFailureSink>();
        Assert.DoesNotContain(sink.Failures, f => f.CorrelationId == "corr-retry");
    }

    [Fact]
    public async Task Exhausted_retries_project_terminal_failure_and_audit_evidence()
    {
        var ct = TestContext.Current.CancellationToken;
        var sink = (InMemoryTerminalFailureSink)fx.Provider.GetRequiredService<ITerminalFailureSink>();

        await fx.Provider.GetRequiredService<IJobScheduler>()
            .EnqueueAsync<AlwaysFailingJob>(new JobContext("tenant-a", "corr-dead"), ct);

        await WaitUntilAsync(() => sink.Failures.Any(f => f.CorrelationId == "corr-dead"), ct, timeoutMs: 30000);
        var failure = Assert.Single(sink.Failures, f => f.CorrelationId == "corr-dead");
        Assert.Equal(ForgeJobWrapper.MaxAttempts, failure.Attempts);
        Assert.Equal("tenant-a", failure.TenantId);

        var audit = fx.Provider.GetRequiredService<IAuditStore>();
        var events = (await audit.ReadAllAsync(ct)).Select(r => r.Event);
        Assert.Contains(events, e => e.Action == "jobs.terminal-failure" && e.CorrelationId == "corr-dead");
    }

    [Fact]
    public void In_memory_store_is_rejected_by_production_validation()
    {
        var failures = fx.Provider.GetServices<IProductionConfigurationValidator>()
            .SelectMany(v => v.Validate());

        Assert.Contains(failures, f => f.Contains("persistent SQL-backed job store", StringComparison.Ordinal));
    }
}
