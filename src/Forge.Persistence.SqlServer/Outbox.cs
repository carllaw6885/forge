using System.Text.Json;
using Forge.Core.Primitives;
using Forge.Events;
using Microsoft.EntityFrameworkCore;

namespace Forge.Persistence.SqlServer;

/// <summary>
/// One pending integration event, stored in the owning module's schema so it
/// commits in the module's own transaction (ADR 04: reliable publication).
/// </summary>
public sealed class OutboxEntry
{
    public long Sequence { get; set; }
    public Guid EventId { get; set; }
    public required string EventType { get; set; }
    public int SchemaVersion { get; set; }
    public string? TenantId { get; set; }
    public required string CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public required string PayloadType { get; set; }
    public required string PayloadJson { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}

/// <summary>
/// IOutbox against the module's own context: EnqueueAsync only adds to the
/// change tracker — the entry commits iff the module's SaveChanges commits.
/// </summary>
public sealed class DbContextOutbox<TContext>(TContext db) : IOutbox where TContext : ForgeModuleDbContext
{
    public Task EnqueueAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        db.Set<OutboxEntry>().Add(new OutboxEntry
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            SchemaVersion = envelope.SchemaVersion,
            TenantId = envelope.TenantId,
            CorrelationId = envelope.CorrelationId.ToString(),
            CausationId = envelope.CausationId,
            PayloadType = envelope.Payload.GetType().AssemblyQualifiedName!,
            PayloadJson = JsonSerializer.Serialize(envelope.Payload, envelope.Payload.GetType()),
            NextAttemptAt = DateTimeOffset.MinValue,
        });
        return Task.CompletedTask;
    }
}

/// <summary>Rehydration helpers shared by the dispatcher and tests.</summary>
public static class OutboxEnvelope
{
    /// <summary>Rehydrates the envelope for dispatch.</summary>
    public static EventEnvelope ToEnvelope(OutboxEntry entry)
    {
        var payloadType = Type.GetType(entry.PayloadType)
            ?? throw new InvalidOperationException($"unknown outbox payload type '{entry.PayloadType}'");
        var payload = (IIntegrationEvent)JsonSerializer.Deserialize(entry.PayloadJson, payloadType)!;
        return new EventEnvelope
        {
            EventId = entry.EventId,
            EventType = entry.EventType,
            SchemaVersion = entry.SchemaVersion,
            TenantId = entry.TenantId,
            CorrelationId = CorrelationId.Parse(entry.CorrelationId),
            CausationId = entry.CausationId,
            Payload = payload,
        };
    }
}
