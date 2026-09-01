using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using {{NAME}}.Notes;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var connectionString = configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");
var command = args.FirstOrDefault() ?? "migrate";

var options = new DbContextOptionsBuilder<NotesDbContext>()
    .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "notes"))
    .Options;
await using var db = new NotesDbContext(options, currentTenant: null);

switch (command)
{
    case "status":
        var pending = (await db.Database.GetPendingMigrationsAsync()).Count();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).Count();
        Console.WriteLine($"notes: {applied} applied, {pending} pending");
        return 0;
    case "migrate":
        await db.Database.MigrateAsync();
        Console.WriteLine("notes: migrated");
        return 0;
    default:
        Console.Error.WriteLine($"error: unknown command '{command}' (expected: migrate | status)");
        return 1;
}
