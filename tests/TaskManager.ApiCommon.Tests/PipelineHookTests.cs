using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.ApiCommon;

namespace TaskManager.ApiCommon.Tests;

public sealed class PipelineHookTests
{
    [Fact]
    public async Task Hook_runs_after_authorization_and_before_endpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Jwt:SigningKey"] = new string('k', 64);
        builder.AddTaskManagerApiDefaults();
        await using var app = builder.Build();

        app.UseTaskManagerApiDefaults(a => a.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Hook"] = "ran";
            await next();
        }));
        app.MapGet("/ping", () => "pong");
        await app.StartAsync();

        var response = await app.GetTestClient().GetAsync("/ping");

        response.Headers.GetValues("X-Hook").ShouldBe(["ran"]);
        (await response.Content.ReadAsStringAsync()).ShouldBe("pong");
    }
}
