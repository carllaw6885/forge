using Forge.Identity;
using Forge.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using {{NAME}}.Notes;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var connectionString = configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");
var command = args.FirstOrDefault() ?? "migrate";

DbContextOptions<TContext> Options<TContext>(string schema) where TContext : DbContext =>
    new DbContextOptionsBuilder<TContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
        .Options;

var contexts = new (string Name, Func<DbContext> Factory)[]
{
    ("notes", () => new NotesDbContext(Options<NotesDbContext>("notes"), currentTenant: null)),
    ("identity", () => new ForgeIdentityDbContext(Options<ForgeIdentityDbContext>("identity"))),
    ("audit", () => new AuditDbContext(Options<AuditDbContext>("audit"))),
    ("settings", () => new SettingsDbContext(Options<SettingsDbContext>("settings"))),
};

switch (command)
{
    case "status":
        foreach (var (name, factory) in contexts)
        {
            await using var db = factory();
            var pending = (await db.Database.GetPendingMigrationsAsync()).Count();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).Count();
            Console.WriteLine($"{name}: {applied} applied, {pending} pending");
        }

        return 0;
    case "migrate":
        foreach (var (name, factory) in contexts)
        {
            await using var db = factory();
            await db.Database.MigrateAsync();
            Console.WriteLine($"{name}: migrated");
        }

        return 0;
    default:
        Console.Error.WriteLine($"error: unknown command '{command}' (expected: migrate | status)");
        return 1;
}
