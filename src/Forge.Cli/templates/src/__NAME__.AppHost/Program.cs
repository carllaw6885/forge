var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql").AddDatabase("forge");
var migrator = builder.AddProject<Projects.{{NAME}}_DbMigrator>("migrator").WithReference(db).WaitFor(db);
// The template ships no launchSettings.json, so declare the endpoint explicitly —
// WithExternalHttpEndpoints alone only flags endpoints, it creates none.
// Aspire is a local-development experience only; without a launch profile the
// api would otherwise default to Production and refuse dev-only configuration.
builder.AddProject<Projects.{{NAME}}_Api>("api")
    .WithReference(db)
    .WaitForCompletion(migrator)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpsEndpoint()
    .WithExternalHttpEndpoints();

builder.Build().Run();
