using Forge.Modularity;
using Forge.Observability;
using Forge.Web;
using {{NAME}}.Notes;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("forge")
    ?? throw new InvalidOperationException("ConnectionStrings__forge is required");

builder.Services.AddProblemDetails();
builder.Services.AddForgeSecurityDefaults(builder.Configuration);
builder.Services.AddForgeTenancy();
builder.Services.AddForgeObservability("{{NAME_LOWER}}");
builder.Services.AddForge(new NotesModule(connectionString));

var app = builder.Build();
app.Services.UseForge();
app.UseForgeSecurityDefaults();
app.UseForgeTenancy();
app.MapForgeHealth(endpoint => endpoint.WithHostScope());
app.MapNotesEndpoints();
app.Run();
