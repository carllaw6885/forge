using Forge.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Forge.Persistence.SqlServer.Tests;

public class ModuleModelValidatorTests
{
    private static DbContextOptions<T> Options<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseSqlServer("Server=unused;Database=unused;Encrypt=false") // model building never connects
            .Options;

    [Fact]
    public void Conforming_module_context_passes()
    {
        using var context = new KernelTestDbContext(Options<KernelTestDbContext>());

        Assert.Empty(ModuleModelValidator.Validate(context, KernelTestDbContext.Manifest));
    }

    [Fact]
    public void Entity_outside_owned_schema_is_rejected()
    {
        using var context = new KernelTestDbContext(Options<KernelTestDbContext>());
        var manifest = KernelTestDbContext.Manifest with { OwnedSchemas = ["somethingelse"] };

        var errors = ModuleModelValidator.Validate(context, manifest);

        Assert.Contains(errors, e => e.Contains("schema 'kerneltest' not owned by module", StringComparison.Ordinal));
    }

    private sealed class ForeignEntityDbContext(DbContextOptions<ForeignEntityDbContext> options)
        : ForgeModuleDbContext(options)
    {
        public override string Schema => "kerneltest";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Maps a CLR type from Forge.Core — exactly the cross-module
            // entity sharing the rule must reject.
            modelBuilder.Entity<Forge.Core.Primitives.Error>().HasNoKey();
        }
    }

    [Fact]
    public void Entity_from_foreign_assembly_is_rejected()
    {
        using var context = new ForeignEntityDbContext(Options<ForeignEntityDbContext>());

        var errors = ModuleModelValidator.Validate(context, KernelTestDbContext.Manifest);

        Assert.Contains(errors, e => e.Contains("never shared across modules", StringComparison.Ordinal));
    }
}
