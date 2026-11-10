using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
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
    options.OnRejected = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = "60";
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Too many sign-in attempts. Please wait a minute and try again.",
        };
        details.Extensions["code"] = "auth.rate_limited";
        await response.WriteAsJsonAsync(details, options: null, contentType: "application/problem+json", ct);
    };
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
