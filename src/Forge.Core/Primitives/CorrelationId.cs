using System.Globalization;

namespace Forge.Core.Primitives;

/// <summary>
/// Identifies one logical operation as it crosses HTTP, events, outbox and jobs
/// (ADR 15). Formats invariantly as 32 lowercase hex digits.
/// </summary>
public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Guid.NewGuid());

    public static CorrelationId Parse(string value) =>
        new(Guid.ParseExact(value, "N"));

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}
