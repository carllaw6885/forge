var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql").AddDatabase("forge");
var migrator = builder.AddProject<Projects.{{NAME}}_DbMigrator>("migrator").WithReference(db).WaitFor(db);
builder.AddProject<Projects.{{NAME}}_Api>("api").WithReference(db).WaitForCompletion(migrator).WithExternalHttpEndpoints();

builder.Build().Run();
