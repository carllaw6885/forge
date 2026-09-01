using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forge.Identity;

/// <summary>Design-time factory so `dotnet ef migrations` can build the model; never used at runtime.</summary>
internal sealed class ForgeIdentityDbContextFactory : IDesignTimeDbContextFactory<ForgeIdentityDbContext>
{
    public ForgeIdentityDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ForgeIdentityDbContext>()
            .UseSqlServer("Server=design-time-only;Database=design;Encrypt=false", sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ForgeIdentityDbContext.Schema))
            .Options);
}
