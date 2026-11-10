using Microsoft.AspNetCore.Builder;
using TaskManager.ApiCommon;
using TaskManager.SharedKernel;

namespace TaskManager.ApiCommon.Tests;

public sealed class ApiDefaultsExtensionsTests
{
    private static WebApplicationBuilder BuilderWithKey(string key)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Jwt:SigningKey"] = key;
        return builder;
    }

    [Fact]
    public void AddTaskManagerApiDefaults_fails_at_startup_when_signing_key_is_too_short()
    {
        Should.Throw<InvalidOperationException>(() => BuilderWithKey("short").AddTaskManagerApiDefaults());
    }

    [Fact]
    public void AddTaskManagerApiDefaults_registers_metrics_and_builds_with_valid_key()
    {
        var builder = BuilderWithKey(new string('k', 32)).AddTaskManagerApiDefaults();

        using var app = builder.Build();

        app.Services.GetService(typeof(IAppMetrics)).ShouldBeOfType<EmfAppMetrics>();
    }
}
