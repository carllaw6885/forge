var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql").AddDatabase("forge");
var migrator = builder.AddProject<Projects.{{NAME}}_DbMigrator>("migrator").WithReference(db).WaitFor(db);
// The template ships no launchSettings.json, so declare the endpoint explicitly —
// WithExternalHttpEndpoints alone only flags endpoints, it creates none.
builder.AddProject<Projects.{{NAME}}_Api>("api")
    .WithReference(db)
    .WaitForCompletion(migrator)
    .WithHttpsEndpoint()
    .WithExternalHttpEndpoints();

builder.Build().Run();
