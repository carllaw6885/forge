using Forge.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Forge.Persistence.SqlServer;

/// <summary>Row shape for the audit schema; append-only at the store contract level.</summary>
public sealed class AuditRecordRow
{
    public long Sequence { get; set; }
    public required string EventJson { get; set; }
    public required string PreviousHash { get; set; }
    public required string Hash { get; set; }
}

/// <summary>
/// The audit trail's own persistence boundary (audit schema). Rows are not
/// tenant-filtered: the trail is cross-tenant evidence and its reads are a
/// host-privileged concern (ADR 08).
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options)
    : ForgeModuleDbContext(options)
{
    public override string Schema => "audit";

    public DbSet<AuditRecordRow> Records => Set<AuditRecordRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AuditRecordRow>(row =>
        {
            row.HasKey(x => x.Sequence);
            row.Property(x => x.Sequence).ValueGeneratedOnAdd();
            row.Property(x => x.PreviousHash).HasMaxLength(64);
            row.Property(x => x.Hash).HasMaxLength(64);
            // A unique PreviousHash makes the chain linear by construction:
            // two concurrent appends claiming the same predecessor cannot both
            // commit — the loser retries on the new head.
            row.HasIndex(x => x.PreviousHash).IsUnique();
        });
    }
}

/// <summary>Hand-written module-owned migration for the audit schema.</summary>
[DbContext(typeof(AuditDbContext))]
[Migration("20260831000004_InitAudit")]
public sealed class InitAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("audit");
        migrationBuilder.CreateTable(
            name: "Records",
            schema: "audit",
            columns: table => new
            {
                Sequence = table.Column<long>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                EventJson = table.Column<string>(nullable: false),
                PreviousHash = table.Column<string>(maxLength: 64, nullable: false),
                Hash = table.Column<string>(maxLength: 64, nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Records", x => x.Sequence));
        migrationBuilder.CreateIndex("IX_Records_PreviousHash", "Records", "PreviousHash", schema: "audit", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Records", schema: "audit");
}

/// <summary>SQL Server reference implementation of the append-only audit store (ADR 08).</summary>
public sealed class SqlServerAuditStore(
    IDbContextFactory<AuditDbContext> contextFactory,
    IAuditRedactionPolicy redaction) : IAuditStore
{
    // Every lost race means another writer committed, so progress is global and
    // retrying is safe; the cap only guards against a pathological store fault.
    private const int MaxAttempts = 64;

    public async Task<AuditRecord> AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        var json = AuditChain.Serialize(auditEvent, redaction);

        for (var attempt = 1; ; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var previous = await db.Records.AsNoTracking()
                .OrderByDescending(r => r.Sequence)
                .Select(r => r.Hash)
                .FirstOrDefaultAsync(cancellationToken) ?? AuditChain.GenesisHash;

            var row = new AuditRecordRow
            {
                EventJson = json,
                PreviousHash = previous,
                Hash = AuditChain.Hash(previous, json),
            };
            db.Records.Add(row);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return new AuditRecord(row.Sequence, row.EventJson, row.PreviousHash, row.Hash);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // lost a head-of-chain race (unique PreviousHash); retry on the new head
                await Task.Delay(Random.Shared.Next(5, 25 * Math.Min(attempt, 8)), cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<AuditRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Records.AsNoTracking()
            .OrderBy(r => r.Sequence)
            .Select(r => new AuditRecord(r.Sequence, r.EventJson, r.PreviousHash, r.Hash))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>DI registration for the SQL Server audit store.</summary>
public static class AuditPersistenceExtensions
{
    public static IServiceCollection AddSqlServerAuditStore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<AuditDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")));
        services.AddSingleton<IAuditStore, SqlServerAuditStore>();
        return services;
    }
}
