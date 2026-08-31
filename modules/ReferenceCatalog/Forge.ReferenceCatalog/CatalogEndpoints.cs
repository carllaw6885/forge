using Forge.Core.Primitives;
using Forge.Events;
using Forge.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Forge.ReferenceCatalog;

/// <summary>Request DTO for creating a catalog item.</summary>
public sealed record CreateCatalogItemRequest(string? Name);

/// <summary>Response DTO; Message is localised (ADR 12).</summary>
public sealed record CatalogItemResponse(Guid Id, string Name, DateTimeOffset CreatedAt, string Message);

/// <summary>
/// The module's Minimal API surface: DTOs in/out, Problem Details, OpenAPI
/// metadata (ADR 16). Tenancy is ambient: the host's tenant resolution
/// middleware runs first (deny-by-default), and EF filters plus write guards
/// enforce isolation centrally — no per-query tenant predicates here (ADR 05).
/// </summary>
public static class CatalogEndpoints
{
    /// <summary>Mapped explicitly by the host (ADR 01) — no endpoint auto-discovery.</summary>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/items").WithTags("Catalog");

        group.MapPost("/", async Task<IResult> (
            CreateCatalogItemRequest request,
            CatalogDbContext db,
            DomainEventCollector domainEvents,
            IIntegrationEventBus bus,
            ICurrentTenant tenant,
            TimeProvider clock,
            IStringLocalizer<CatalogResources> localizer,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 128)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Name)] = [localizer["NameInvalid"]],
                });
            }

            var correlation = CorrelationId.New();
            var item = new CatalogItem
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                CreatedAt = clock.GetUtcNow(),
            };

            db.Items.Add(item); // TenantId stamped centrally on save
            await db.SaveChangesAsync(ct);
            domainEvents.Raise(new CatalogItemAdded(item.Id, item.Name, item.TenantId, correlation));
            await domainEvents.DispatchAsync(ct);

            // ponytail: direct in-process publish; the transactional outbox
            // replaces this call in Phase 3 (IOutbox contract already exists).
            await bus.PublishAsync(
                EventEnvelope.Create(new CatalogItemCreated(item.Id, item.Name), correlation, tenantId: tenant.Id),
                ct);

            var response = new CatalogItemResponse(item.Id, item.Name, item.CreatedAt, localizer["ItemCreated"]);
            return TypedResults.Created($"/api/catalog/items/{item.Id}", response);
        })
        .WithName("CreateCatalogItem")
        .Produces<CatalogItemResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async Task<IResult> (
            Guid id,
            CatalogDbContext db,
            IStringLocalizer<CatalogResources> localizer,
            CancellationToken ct) =>
        {
            var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            return item is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(new CatalogItemResponse(item.Id, item.Name, item.CreatedAt, localizer["ItemFound"]));
        })
        .WithName("GetCatalogItem")
        .Produces<CatalogItemResponse>()
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
