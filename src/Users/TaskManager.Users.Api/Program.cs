using Amazon.SimpleSystemsManagement;
using TaskManager.ApiCommon;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Users.Api;
using TaskManager.Users.Application;
using TaskManager.Users.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// In AWS the demo user's private password comes from SSM (Seed:DemoPasswordParameterName).
builder.Configuration.AddFromSsm(
    () => new AmazonSimpleSystemsManagementClient(), "Seed:DemoPasswordParameterName", "Seed:DemoPassword");

builder.AddTaskManagerApiDefaults();
builder.Services.AddUsersApplication().AddUsersInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DynamoTableHealthCheck>("dynamodb", tags: [ApiDefaultsExtensions.ReadyTag]);
builder.Services.AddAuthRateLimiting(builder.Configuration);

var app = builder.Build();

app.UseTaskManagerApiDefaults(a => a.UseRateLimiter());

app.Run();

public partial class Program;
