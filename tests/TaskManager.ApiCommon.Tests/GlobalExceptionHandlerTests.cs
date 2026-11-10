using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;
using TaskManager.ApiCommon;

namespace TaskManager.ApiCommon.Tests;

public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(bool handled, DefaultHttpContext ctx, JsonElement body)> Run(string environmentName)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, env);
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.TraceIdentifier = "trace-123";

        var handled = await handler.TryHandleAsync(ctx, new InvalidOperationException("secret detail"), CancellationToken.None);

        ctx.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(ctx.Response.Body);
        return (handled, ctx, body.RootElement.Clone());
    }

    [Fact]
    public async Task GlobalExceptionHandler_returns_500_without_exception_detail_outside_development()
    {
        var (handled, ctx, body) = await Run(Environments.Production);

        handled.ShouldBeTrue();
        ctx.Response.StatusCode.ShouldBe(500);
        body.GetProperty("title").GetString().ShouldBe("An unexpected error occurred.");
        body.GetProperty("traceId").GetString().ShouldBe("trace-123");
        body.TryGetProperty("exception", out _).ShouldBeFalse();
        body.ToString().ShouldNotContain("secret detail");
    }

    [Fact]
    public async Task GlobalExceptionHandler_includes_exception_detail_in_development()
    {
        var (_, _, body) = await Run(Environments.Development);

        body.GetProperty("exception").GetString()!.ShouldContain("secret detail");
    }
}
