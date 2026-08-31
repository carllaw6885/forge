namespace Forge.Core.Privacy;

/// <summary>Data classification levels (ADR 09). Explicit, machine-readable, and respected by audit/storage/template paths.</summary>
public enum DataClassification
{
    Public,
    Internal,
    Personal,
    Sensitive,
}

/// <summary>Retention classes (ADR 09): how long a class of data is kept, absent a legal hold.</summary>
public sealed record RetentionPolicy(string Name, TimeSpan? RetainFor)
{
    public static readonly RetentionPolicy Ephemeral = new("ephemeral", TimeSpan.FromDays(30));
    public static readonly RetentionPolicy Standard = new("standard", TimeSpan.FromDays(365 * 2));
    public static readonly RetentionPolicy LongTerm = new("long-term", TimeSpan.FromDays(365 * 7));
    public static readonly RetentionPolicy Indefinite = new("indefinite", null);
}

/// <summary>
/// Legal hold flag model (ADR 09): while a hold covering a subject exists,
/// retention-driven deletion is suspended. Holds are placed and released
/// explicitly and are themselves auditable actions.
/// </summary>
public sealed record LegalHold(string Id, string Subject, string Reason, DateTimeOffset PlacedAt);

/// <summary>Marks a member (or type) with its data classification (ADR 09).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Parameter)]
public sealed class ClassifiedAttribute(DataClassification classification) : Attribute
{
    public DataClassification Classification { get; } = classification;
}

/// <summary>One piece of a subject's personal data, as enumerated by a module.</summary>
public sealed record PersonalDataItem(
    string Module, string Subject, string Name, DataClassification Classification, string Value);

/// <summary>
/// Privacy contributor contract (ADR 09): a module enumerates the personal data
/// it holds for a subject. Subject-right workflows compose these; the full GDPR
/// workbench is post-v0.1.
/// </summary>
public interface IPrivacyContributor
{
    Task<IReadOnlyList<PersonalDataItem>> EnumeratePersonalDataAsync(string subjectId, CancellationToken cancellationToken);
}
