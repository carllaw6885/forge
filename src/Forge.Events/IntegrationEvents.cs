using System.Reflection;
using Forge.Core.Primitives;

namespace Forge.Events;

/// <summary>
/// A versioned fact that crosses module or system boundaries (ADR 04): pure
/// data, no behaviour, contract owned by the publishing module. Breaking payload
/// changes require a new schema version.
/// </summary>
public interface IIntegrationEvent;

/// <summary>Declares the stable wire name and schema version of an integration event contract.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class IntegrationEventAttribute(string name, int schemaVersion) : Attribute
{
    /// <summary>Stable dotted wire name, e.g. "catalog.item.created".</summary>
    public string Name { get; } = name;

    public int SchemaVersion { get; } = schemaVersion;
}

/// <summary>
/// The envelope every integration event travels in: event identity for
/// duplicate-tolerant delivery, tenant and correlation/causation context that
/// must survive dispatch (ADRs 05/15), and the versioned payload.
/// </summary>
public sealed record EventEnvelope
{
    public required Guid EventId { get; init; }
    public required string EventType { get; init; }
    public required int SchemaVersion { get; init; }
    public required string? TenantId { get; init; }
    public required CorrelationId CorrelationId { get; init; }

    /// <summary>EventId of the event that caused this one, if any.</summary>
    public required Guid? CausationId { get; init; }

    public required IIntegrationEvent Payload { get; init; }

    public static EventEnvelope Create(
        IIntegrationEvent payload,
        CorrelationId correlationId,
        string? tenantId = null,
        Guid? causationId = null)
    {
        var attribute = payload.GetType().GetCustomAttribute<IntegrationEventAttribute>()
            ?? throw new InvalidOperationException(
                $"integration event '{payload.GetType().FullName}' is missing [IntegrationEvent(name, schemaVersion)]");

        return new EventEnvelope
        {
            EventId = Guid.NewGuid(),
            EventType = attribute.Name,
            SchemaVersion = attribute.SchemaVersion,
            TenantId = tenantId,
            CorrelationId = correlationId,
            CausationId = causationId,
            Payload = payload,
        };
    }
}
