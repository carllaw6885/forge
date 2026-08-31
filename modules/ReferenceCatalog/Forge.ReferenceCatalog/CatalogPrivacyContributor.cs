using Forge.Core.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Forge.ReferenceCatalog;

/// <summary>
/// The module's privacy contributor (ADR 09 acceptance demonstration):
/// enumerates the personal data the catalog holds for a subject. Runs under
/// ambient tenancy, so it can only ever see the current tenant's data.
/// </summary>
internal sealed class CatalogPrivacyContributor(CatalogDbContext db) : IPrivacyContributor
{
    public async Task<IReadOnlyList<PersonalDataItem>> EnumeratePersonalDataAsync(
        string subjectId, CancellationToken cancellationToken)
    {
        var items = await db.Items.AsNoTracking()
            .Where(i => i.CreatedBy == subjectId)
            .Select(i => new { i.Id, i.CreatedBy })
            .ToListAsync(cancellationToken);

        return items.Select(i => new PersonalDataItem(
            Module: "Forge.ReferenceCatalog",
            Subject: subjectId,
            Name: $"catalog-item:{i.Id:N}:CreatedBy",
            Classification: DataClassification.Personal,
            Value: i.CreatedBy!)).ToList();
    }
}
