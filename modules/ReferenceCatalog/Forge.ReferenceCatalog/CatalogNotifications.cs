using Forge.Events;
using Forge.Notifications;
using Forge.Templates;

namespace Forge.ReferenceCatalog;

/// <summary>The module's constrained notification template (ADRs 11/38), localised with neutral fallback.</summary>
public static class CatalogTemplates
{
    public static readonly Template ItemCreated = new(
        "catalog.item-created", Version: 1,
        new Dictionary<string, string>
        {
            [Template.NeutralCulture] = "Your catalogue item {{item}} was created.",
            ["ar"] = "تم إنشاء عنصر الكتالوج {{item}} الخاص بك.",
        },
        new HashSet<string> { "item" });
}

/// <summary>
/// Sends the creator an in-app notification through the constrained template,
/// honouring preferences (ADR 11 demonstration).
/// </summary>
internal sealed class CatalogItemAddedNotificationHandler(NotificationService notifications)
    : IDomainEventHandler<CatalogItemAdded>
{
    public async Task HandleAsync(CatalogItemAdded domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.CreatedBy is null)
        {
            return; // nobody to notify
        }

        await notifications.SendAsync(
            new NotificationIntent(
                Type: "catalog.item-created",
                Recipient: domainEvent.CreatedBy,
                TenantId: domainEvent.TenantId,
                CorrelationId: domainEvent.CorrelationId.ToString(),
                Variables: new Dictionary<string, string> { ["item"] = domainEvent.Name }),
            CatalogTemplates.ItemCreated,
            System.Globalization.CultureInfo.CurrentUICulture.Name,
            cancellationToken);
    }
}
