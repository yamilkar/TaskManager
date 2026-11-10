using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.ApiCommon;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Users.Application;
using TaskManager.Users.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddTaskManagerApiDefaults();
builder.Services.AddUsersApplication().AddUsersInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DynamoTableHealthCheck>("dynamodb", tags: [ApiDefaultsExtensions.ReadyTag]);

var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

app.UseTaskManagerApiDefaults(a => a.UseRateLimiter());

app.Run();

public partial class Program;
