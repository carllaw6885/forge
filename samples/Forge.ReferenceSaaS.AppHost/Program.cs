// Aspire reference topology (ADR 25): SQL Server, the migrator (runs first),
// the app, and the dashboard's telemetry — local development only; production
// is the bare OCI container and needs none of this.

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var db = sql.AddDatabase("forge");

var migrator = builder.AddProject<Projects.Forge_ReferenceSaaS_DbMigrator>("migrator")
    .WithReference(db)
    .WaitFor(db);

builder.AddProject<Projects.Forge_ReferenceSaaS_Api>("api")
    .WithReference(db)
    .WaitForCompletion(migrator)
    .WithExternalHttpEndpoints();

builder.Build().Run();
