using Forge.Identity;
using Forge.Persistence.SqlServer;
using Forge.ReferenceCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

// Independent migration runner (ADR 25): migrations execute here, never at web
// startup. Owns every module schema plus the Quartz job store schema.
// Commands: migrate (default) | status. Deterministic output, idempotent.

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var connectionString = configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");
var command = args.FirstOrDefault() ?? "migrate";

DbContextOptions<TContext> Options<TContext>(string schema) where TContext : DbContext =>
    new DbContextOptionsBuilder<TContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
        .Options;

var migratedContexts = new (string Name, Func<Task> Migrate, Func<Task<string>> Status)[]
{
    Context("catalog", () => new CatalogDbContext(Options<CatalogDbContext>("catalog"), currentTenant: null!)),
    Context("audit", () => new AuditDbContext(Options<AuditDbContext>("audit"))),
    Context("settings", () => new SettingsDbContext(Options<SettingsDbContext>("settings"))),
};

switch (command)
{
    case "status":
        foreach (var (name, _, status) in migratedContexts)
        {
            Console.WriteLine($"{name}: {await status()}");
        }

        Console.WriteLine("identity: managed via CreateTables (idempotent)");
        Console.WriteLine("jobs: managed via Quartz schema installer (idempotent)");
        return 0;

    case "migrate":
        foreach (var (name, migrate, _) in migratedContexts)
        {
            await migrate();
            Console.WriteLine($"{name}: migrated");
        }

        await using (var identity = new ForgeIdentityDbContext(Options<ForgeIdentityDbContext>("identity")))
        {
            var creator = identity.GetService<IRelationalDatabaseCreator>();
            try
            {
                await creator.CreateTablesAsync();
                Console.WriteLine("identity: tables created");
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                Console.WriteLine("identity: tables already present");
            }
        }

        await Forge.Jobs.Quartz.ServiceCollectionExtensions.EnsureQuartzSchemaAsync(connectionString, CancellationToken.None);
        Console.WriteLine("jobs: quartz schema ensured");
        return 0;

    default:
        Console.Error.WriteLine($"error: unknown command '{command}' (expected: migrate | status)");
        return 1;
}

static (string, Func<Task>, Func<Task<string>>) Context<TContext>(string name, Func<TContext> factory)
    where TContext : DbContext =>
    (name,
     async () =>
     {
         await using var db = factory();
         await db.Database.MigrateAsync();
     },
     async () =>
     {
         await using var db = factory();
         var pending = (await db.Database.GetPendingMigrationsAsync()).Count();
         var applied = (await db.Database.GetAppliedMigrationsAsync()).Count();
         return $"{applied} applied, {pending} pending";
     }
);
