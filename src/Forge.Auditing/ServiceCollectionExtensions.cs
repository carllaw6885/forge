using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Auditing;

/// <summary>DI registration for structured auditing.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the redaction policy and the in-memory reference store.
    /// Hosts replace the store with the SQL Server one via
    /// AddSqlServerAuditStore (Forge.Persistence.SqlServer).
    /// </summary>
    public static IServiceCollection AddForgeAuditing(this IServiceCollection services)
    {
        services.TryAddSingleton<IAuditRedactionPolicy, DefaultAuditRedactionPolicy>();
        services.TryAddSingleton<IAuditStore, InMemoryAuditStore>();
        return services;
    }
}
