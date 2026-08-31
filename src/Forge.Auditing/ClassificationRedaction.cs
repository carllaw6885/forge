using Forge.Core.Privacy;

namespace Forge.Auditing;

/// <summary>
/// Classification-aware redaction (ADR 09): audit detail keys a module declares
/// as Personal or Sensitive are redacted before storage, on top of the default
/// secret-fragment deny list.
/// </summary>
public sealed class ClassificationAwareRedactionPolicy(
    IReadOnlyDictionary<string, DataClassification> detailClassifications) : IAuditRedactionPolicy
{
    private readonly DefaultAuditRedactionPolicy _defaults = new();

    public bool IsSensitive(string detailKey) =>
        _defaults.IsSensitive(detailKey)
        || (detailClassifications.TryGetValue(detailKey, out var classification)
            && classification is DataClassification.Personal or DataClassification.Sensitive);
}
