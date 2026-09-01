using Forge.Core.Modules;
using Forge.Modularity;
using Forge.Persistence.SqlServer;
using Forge.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace {{NAME}}.Notes;

/// <summary>Tenant-owned note; filtered and stamped centrally by Forge.</summary>
public sealed class Note : ITenantOwned
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public required string Text { get; set; }
}

public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options, ICurrentTenant? currentTenant)
    : ForgeModuleDbContext(options, currentTenant)
{
    public override string Schema => "notes";

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Note>(n => n.Property(x => x.Text).HasMaxLength(512));
    }
}

[DbContext(typeof(NotesDbContext))]
[Migration("00000000000001_InitNotes")]
public sealed class InitNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("notes");
        migrationBuilder.CreateTable(
            name: "Notes",
            schema: "notes",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TenantId = table.Column<string>(maxLength: 64, nullable: false),
                Text = table.Column<string>(maxLength: 512, nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Notes", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Notes", schema: "notes");
}

/// <summary>Composed explicitly by the host: AddForge(new NotesModule(connectionString)).</summary>
public sealed class NotesModule(string connectionString) : IForgeModule
{
    public ModuleManifest Manifest { get; } = new()
    {
        Id = "{{NAME}}.Notes",
        Name = "Notes",
        Version = "0.1.0",
        OwnedSchemas = ["notes"],
    };

    public void ConfigureServices(IServiceCollection services) =>
        services.AddModuleDbContext<NotesDbContext>(connectionString, schema: "notes");
}

public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notes").WithTags("Notes");
        group.MapGet("/", async (NotesDbContext db, CancellationToken ct) =>
            await db.Notes.AsNoTracking().Select(n => new { n.Id, n.Text }).ToListAsync(ct));
        group.MapPost("/", async Task<IResult> (NoteRequest request, NotesDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Text is required."] });
            }

            var note = new Note { Id = Guid.NewGuid(), Text = request.Text };
            db.Notes.Add(note);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/api/notes/{note.Id}", new { note.Id, note.Text });
        });
        return app;
    }
}

public sealed record NoteRequest(string? Text);
