using Forge.Core.Modules;
using Forge.Events;
using Forge.Modularity;
using Forge.Persistence.SqlServer;
using Forge.ReferenceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Forge.ReferenceCatalog;

/// <summary>Explicitly composed by the host: AddForge(new CatalogModule(connectionString)).</summary>
public sealed class CatalogModule(string connectionString) : IForgeModule
{
    public ModuleManifest Manifest { get; } = new()
    {
        Id = "Forge.ReferenceCatalog",
        Name = "Reference Catalog",
        Version = "0.1.0",
        OwnedSchemas = ["catalog"],
    };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddModuleDbContext<CatalogDbContext>(connectionString, schema: "catalog");
        services.AddForgeEvents();
        services.AddScoped<ICatalogReader, CatalogReader>();
        services.AddSingleton<ICatalogAuditTrail, InMemoryCatalogAuditTrail>();
        services.AddScoped<IDomainEventHandler<CatalogItemAdded>, CatalogItemAddedAuditHandler>();
    }
}
