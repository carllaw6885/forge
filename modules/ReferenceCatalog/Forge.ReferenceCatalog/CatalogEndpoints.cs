using Forge.Core.Primitives;
using Forge.Events;
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

/// <summary>The module's Minimal API surface: DTOs in/out, Problem Details, OpenAPI metadata (ADR 16).</summary>
public static class CatalogEndpoints
{
    // Tenant context seam: Phase 2 replaces the raw header with the trusted
    // resolution pipeline; missing tenant is already deny-by-default here.
    private const string TenantHeader = "X-Tenant";

    /// <summary>Mapped explicitly by the host (ADR 01) — no endpoint auto-discovery.</summary>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog/items").WithTags("Catalog");

        group.MapPost("/", async Task<IResult> (
            CreateCatalogItemRequest request,
            HttpContext http,
            CatalogDbContext db,
            DomainEventCollector domainEvents,
            IIntegrationEventBus bus,
            TimeProvider clock,
            IStringLocalizer<CatalogResources> localizer,
            CancellationToken ct) =>
        {
            if (GetTenant(http) is not { } tenant)
            {
                return MissingTenant();
            }

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
                TenantId = tenant,
                Name = request.Name.Trim(),
                CreatedAt = clock.GetUtcNow(),
            };

            db.Items.Add(item);
            domainEvents.Raise(new CatalogItemAdded(item.Id, item.Name, tenant, correlation));
            await db.SaveChangesAsync(ct);
            await domainEvents.DispatchAsync(ct);

            // ponytail: direct in-process publish; the transactional outbox
            // replaces this call in Phase 3 (IOutbox contract already exists).
            await bus.PublishAsync(
                EventEnvelope.Create(new CatalogItemCreated(item.Id, item.Name), correlation, tenantId: tenant),
                ct);

            var response = new CatalogItemResponse(item.Id, item.Name, item.CreatedAt, localizer["ItemCreated"]);
            return TypedResults.Created($"/api/catalog/items/{item.Id}", response);
        })
        .WithName("CreateCatalogItem")
        .Produces<CatalogItemResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async Task<IResult> (
            Guid id,
            HttpContext http,
            CatalogDbContext db,
            IStringLocalizer<CatalogResources> localizer,
            CancellationToken ct) =>
        {
            if (GetTenant(http) is not { } tenant)
            {
                return MissingTenant();
            }

            var item = await db.Items.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct);
            return item is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(new CatalogItemResponse(item.Id, item.Name, item.CreatedAt, localizer["ItemFound"]));
        })
        .WithName("GetCatalogItem")
        .Produces<CatalogItemResponse>()
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static string? GetTenant(HttpContext http) =>
        http.Request.Headers.TryGetValue(TenantHeader, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult MissingTenant() =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing tenant context", detail: $"The {TenantHeader} header is required.");
}
