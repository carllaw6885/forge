using Forge.Core.Modules;
using Microsoft.EntityFrameworkCore;

namespace Forge.Persistence.SqlServer;

/// <summary>
/// Validates a module's compiled EF model against its manifest: every mapped
/// table/view must sit in a schema the module owns, and every entity CLR type
/// must live in the context's own assembly — domain entities are never shared
/// across modules (ADRs 03/04). Cross-module foreign keys cannot exist when
/// every table a context maps is schema-owned, which is what this proves.
/// </summary>
public static class ModuleModelValidator
{
    /// <summary>Deterministic, ordered error list; empty means the model conforms.</summary>
    public static IReadOnlyList<string> Validate(DbContext context, ModuleManifest manifest)
    {
        var owned = new HashSet<string>(manifest.OwnedSchemas, StringComparer.OrdinalIgnoreCase);
        var contextAssembly = context.GetType().Assembly;
        var errors = new List<string>();

        foreach (var entity in context.Model.GetEntityTypes().OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            var schema = entity.GetSchema() ?? entity.GetViewSchema() ?? context.Model.GetDefaultSchema();
            if (schema is null || !owned.Contains(schema))
            {
                errors.Add($"entity '{entity.ClrType.Name}' maps to schema '{schema ?? "(default)"}' not owned by module '{manifest.Id}'");
            }

            if (entity.ClrType.Assembly == typeof(ForgeModuleDbContext).Assembly)
            {
                continue; // infrastructure-owned shapes (e.g. the outbox) are not domain entities
            }

            if (entity.ClrType.Assembly != contextAssembly)
            {
                errors.Add($"entity '{entity.ClrType.FullName}' lives in '{entity.ClrType.Assembly.GetName().Name}', not the module assembly '{contextAssembly.GetName().Name}' — domain entities are never shared across modules");
            }
        }

        return errors;
    }
}
