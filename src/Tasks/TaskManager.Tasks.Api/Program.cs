using TaskManager.ApiCommon;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Tasks.Application;
using TaskManager.Tasks.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTaskManagerApiDefaults();
builder.Services.AddTasksApplication().AddTasksInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DynamoTableHealthCheck>("dynamodb", tags: [ApiDefaultsExtensions.ReadyTag]);

var app = builder.Build();

app.UseTaskManagerApiDefaults();

app.Run();

public partial class Program;
