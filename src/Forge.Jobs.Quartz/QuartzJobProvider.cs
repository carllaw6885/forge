using System.Diagnostics;
using Forge.Auditing;
using Forge.Core.Validation;
using Forge.Jobs;
using Forge.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace Forge.Jobs.Quartz;

/// <summary>
/// The single Quartz job type: resolves the declared IForgeJob from DI,
/// restores tenant and correlation context (ADR 10), retries with backoff, and
/// projects terminal failure to the sink plus an audit event on exhaustion.
/// </summary>
[PersistJobDataAfterExecution]
[DisallowConcurrentExecution]
public sealed class ForgeJobWrapper(
    IServiceProvider services,
    CurrentTenant currentTenant,
    ITerminalFailureSink terminalFailures,
    IAuditStore audit,
    TimeProvider clock) : IJob
{
    private static readonly ActivitySource ActivitySource = new("Forge.Jobs");

    public const int MaxAttempts = 3;

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var jobTypeName = data.GetString("forge:jobType")!;
        static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
        var jobContext = new JobContext(
            NullIfEmpty(data.GetString("forge:tenantId")),
            data.GetString("forge:correlationId")!,
            NullIfEmpty(data.GetString("forge:idempotencyKey")));

        var jobType = Type.GetType(jobTypeName)
            ?? throw new JobExecutionException($"unknown job type '{jobTypeName}'") { UnscheduleAllTriggers = true };

        var parent = data.GetString("forge:traceparent") is { Length: > 0 } tp
            && ActivityContext.TryParse(tp, null, out var parsed) ? parsed : default;
        using var activity = ActivitySource.StartActivity("job.execute", ActivityKind.Consumer, parent);
        activity?.SetTag("forge.job_type", jobTypeName);
        activity?.SetTag("forge.tenant_id", jobContext.TenantId);
        activity?.SetTag("forge.correlation_id", jobContext.CorrelationId);

        // restore ambient tenant context for the job's flow
        if (jobContext.TenantId is not null)
        {
            currentTenant.SetTenant(jobContext.TenantId);
        }

        try
        {
            using var scope = services.CreateScope();
            var job = (IForgeJob)scope.ServiceProvider.GetRequiredService(jobType);
            await job.ExecuteAsync(jobContext, context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var attempts = (data.TryGetValue("forge:attempts", out var raw) ? Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture) : 0) + 1;
            context.JobDetail.JobDataMap["forge:attempts"] = attempts;

            if (attempts < MaxAttempts)
            {
                // reschedule this execution with exponential backoff
                var retry = TriggerBuilder.Create()
                    .ForJob(context.JobDetail)
                    .StartAt(clock.GetUtcNow().AddSeconds(Math.Pow(2, attempts)))
                    .Build();
                await context.Scheduler.ScheduleJob(retry, context.CancellationToken);
                return;
            }

            var failure = new TerminalJobFailure(
                jobTypeName, jobContext.TenantId, jobContext.CorrelationId, ex.Message, attempts, clock.GetUtcNow());
            await terminalFailures.RecordAsync(failure, context.CancellationToken);
            await audit.AppendAsync(new AuditEvent
            {
                Action = "jobs.terminal-failure",
                TenantId = jobContext.TenantId,
                Actor = "system",
                CorrelationId = jobContext.CorrelationId,
                Subject = jobTypeName,
                Outcome = "failure",
                OccurredAt = clock.GetUtcNow(),
                Details = new Dictionary<string, string>
                {
                    ["attempts"] = attempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["error"] = ex.Message,
                },
            }, context.CancellationToken);
        }
    }
}

/// <summary>IJobScheduler over Quartz. Idempotency keys become job identities: duplicates are no-ops.</summary>
public sealed class QuartzJobScheduler(ISchedulerFactory schedulerFactory) : IJobScheduler
{
    public async Task EnqueueAsync<TJob>(JobContext context, CancellationToken cancellationToken)
        where TJob : IForgeJob
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var key = new JobKey(context.IdempotencyKey ?? Guid.NewGuid().ToString("N"), typeof(TJob).Name);

        if (context.IdempotencyKey is not null && await scheduler.CheckExists(key, cancellationToken))
        {
            return; // duplicate enqueue of the same logical work (ADR 10)
        }

        var job = JobBuilder.Create<ForgeJobWrapper>()
            .WithIdentity(key)
            .UsingJobData("forge:jobType", typeof(TJob).AssemblyQualifiedName!)
            .UsingJobData("forge:tenantId", context.TenantId ?? string.Empty)
            .UsingJobData("forge:correlationId", context.CorrelationId)
            .UsingJobData("forge:idempotencyKey", context.IdempotencyKey ?? string.Empty)
            .UsingJobData("forge:traceparent", Activity.Current?.Id ?? string.Empty)
            .StoreDurably()
            .Build();
        var trigger = TriggerBuilder.Create().ForJob(job).StartNow().Build();

        try
        {
            await scheduler.ScheduleJob(job, trigger, cancellationToken);
        }
        catch (ObjectAlreadyExistsException)
        {
            // lost an idempotency race: same key enqueued concurrently — no-op
        }
    }

    public async Task ScheduleRecurringAsync<TJob>(string cronExpression, JobContext context, CancellationToken cancellationToken)
        where TJob : IForgeJob
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var job = JobBuilder.Create<ForgeJobWrapper>()
            .WithIdentity(new JobKey($"recurring:{typeof(TJob).Name}", typeof(TJob).Name))
            .UsingJobData("forge:jobType", typeof(TJob).AssemblyQualifiedName!)
            .UsingJobData("forge:tenantId", context.TenantId ?? string.Empty)
            .UsingJobData("forge:correlationId", context.CorrelationId)
            .StoreDurably()
            .Build();
        var trigger = TriggerBuilder.Create().ForJob(job)
            .WithCronSchedule(cronExpression)
            .Build();
        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }
}

/// <summary>Rejects the in-memory job store under production validation (ADR 10).</summary>
internal sealed class DurableJobStoreValidator(ForgeQuartzOptions options) : IProductionConfigurationValidator
{
    public IEnumerable<string> Validate()
    {
        if (options.UseInMemoryStore)
        {
            yield return "Quartz must use the persistent SQL-backed job store in production, not the in-memory store";
        }
    }
}

/// <summary>Quartz provider settings: in-memory store for development, SQL store (connection string) for durable jobs.</summary>
public sealed record ForgeQuartzOptions
{
    public bool UseInMemoryStore { get; init; }
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Scheduler name; Quartz's scheduler repository is process-wide, so tests
    /// hosting several containers must use distinct names. Keep it stable in
    /// production (clustering identity).
    /// </summary>
    public string SchedulerName { get; init; } = "forge";
}
