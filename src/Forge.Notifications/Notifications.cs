using System.Collections.Concurrent;
using Forge.Auditing;
using Forge.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Notifications;

/// <summary>A channel-neutral notification intent (ADR 11): what to tell whom, never how.</summary>
public sealed record NotificationIntent(
    string Type,
    string Recipient,
    string? TenantId,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Variables,
    bool SecurityCritical = false);

/// <summary>Terminal outcome of one notification delivery attempt.</summary>
public enum DeliveryState
{
    Delivered,
    Suppressed,
    Failed,
}

/// <summary>Durable delivery-state record (ADR 11): every intent leaves evidence of what happened to it.</summary>
public sealed record DeliveryRecord(
    Guid Id, string Type, string Recipient, string? TenantId, string Channel,
    DeliveryState State, string? Error, DateTimeOffset At);

/// <summary>Stores delivery state durably; in-memory reference here, SQL Server in Forge.Persistence.SqlServer.</summary>
public interface IDeliveryStateStore
{
    Task AppendAsync(DeliveryRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliveryRecord>> ListForAsync(string recipient, CancellationToken cancellationToken);
}

internal sealed class InMemoryDeliveryStateStore : IDeliveryStateStore
{
    private readonly ConcurrentQueue<DeliveryRecord> _records = new();

    public Task AppendAsync(DeliveryRecord record, CancellationToken cancellationToken)
    {
        _records.Enqueue(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeliveryRecord>> ListForAsync(string recipient, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeliveryRecord>>([.. _records.Where(r => r.Recipient == recipient)]);
}

/// <summary>Delivery adapter (ADR 11): in-app, email, SMS and push are adapters — Core never couples to a vendor.</summary>
public interface INotificationChannel
{
    string Name { get; }

    Task DeliverAsync(string recipient, string body, CancellationToken cancellationToken);
}

/// <summary>Reference in-app channel: messages are queryable per recipient.</summary>
public sealed class InAppChannel : INotificationChannel
{
    private readonly ConcurrentDictionary<string, List<string>> _inboxes = new(StringComparer.Ordinal);

    public string Name => "in-app";

    public Task DeliverAsync(string recipient, string body, CancellationToken cancellationToken)
    {
        _inboxes.GetOrAdd(recipient, _ => []).Add(body);
        return Task.CompletedTask;
    }

    public IReadOnlyList<string> InboxOf(string recipient) =>
        _inboxes.TryGetValue(recipient, out var inbox) ? [.. inbox] : [];
}

/// <summary>Recipient preferences (ADR 11): opt-outs per notification type.</summary>
public interface INotificationPreferences
{
    Task<bool> IsOptedOutAsync(string recipient, string type, CancellationToken cancellationToken);

    Task SetOptOutAsync(string recipient, string type, bool optedOut, CancellationToken cancellationToken);
}

internal sealed class InMemoryNotificationPreferences : INotificationPreferences
{
    private readonly ConcurrentDictionary<(string, string), bool> _optOuts = new();

    public Task<bool> IsOptedOutAsync(string recipient, string type, CancellationToken cancellationToken) =>
        Task.FromResult(_optOuts.GetValueOrDefault((recipient, type)));

    public Task SetOptOutAsync(string recipient, string type, bool optedOut, CancellationToken cancellationToken)
    {
        _optOuts[(recipient, type)] = optedOut;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Sends intents through constrained templates (ADR 11/38): preferences are
/// honoured unless the intent is security-critical — that policy override is
/// itself audited — and every outcome lands in durable delivery state.
/// </summary>
public sealed class NotificationService(
    INotificationChannel channel,
    INotificationPreferences preferences,
    IDeliveryStateStore deliveryState,
    IAuditStore audit,
    TimeProvider clock)
{
    public async Task<DeliveryRecord> SendAsync(
        NotificationIntent intent, Template template, string culture, CancellationToken cancellationToken)
    {
        if (!intent.SecurityCritical
            && await preferences.IsOptedOutAsync(intent.Recipient, intent.Type, cancellationToken))
        {
            return await RecordAsync(intent, DeliveryState.Suppressed, error: null, cancellationToken);
        }

        if (intent.SecurityCritical
            && await preferences.IsOptedOutAsync(intent.Recipient, intent.Type, cancellationToken))
        {
            // policy override: security-critical communications ignore opt-outs, audibly (ADR 11)
            await audit.AppendAsync(new AuditEvent
            {
                Action = "notifications.policy-override",
                TenantId = intent.TenantId,
                Actor = "system",
                CorrelationId = intent.CorrelationId,
                Subject = intent.Recipient,
                Outcome = "success",
                OccurredAt = clock.GetUtcNow(),
                Details = new Dictionary<string, string> { ["type"] = intent.Type },
            }, cancellationToken);
        }

        try
        {
            var body = TemplateRenderer.Render(template, culture, intent.Variables);
            await channel.DeliverAsync(intent.Recipient, body, cancellationToken);
            return await RecordAsync(intent, DeliveryState.Delivered, error: null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await RecordAsync(intent, DeliveryState.Failed, ex.Message, cancellationToken);
        }
    }

    private async Task<DeliveryRecord> RecordAsync(
        NotificationIntent intent, DeliveryState state, string? error, CancellationToken cancellationToken)
    {
        var record = new DeliveryRecord(
            Guid.NewGuid(), intent.Type, intent.Recipient, intent.TenantId, channel.Name, state, error, clock.GetUtcNow());
        await deliveryState.AppendAsync(record, cancellationToken);
        return record;
    }
}

/// <summary>DI registration for notifications with the in-app reference channel.</summary>
public static class NotificationsExtensions
{
    public static IServiceCollection AddForgeNotifications(this IServiceCollection services)
    {
        services.TryAddSingleton<InAppChannel>();
        services.TryAddSingleton<INotificationChannel>(sp => sp.GetRequiredService<InAppChannel>());
        services.TryAddSingleton<INotificationPreferences, InMemoryNotificationPreferences>();
        services.TryAddSingleton<IDeliveryStateStore, InMemoryDeliveryStateStore>();
        services.TryAddScoped<NotificationService>();
        return services;
    }
}
