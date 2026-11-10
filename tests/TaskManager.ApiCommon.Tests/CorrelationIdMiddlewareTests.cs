using Microsoft.AspNetCore.Http;
using TaskManager.ApiCommon;

namespace TaskManager.ApiCommon.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    private static async Task<string> Invoke(string? incoming)
    {
        var ctx = new DefaultHttpContext();
        if (incoming is not null)
        {
            ctx.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
        }

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(ctx);
        return ctx.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
    }

    [Fact]
    public async Task CorrelationId_is_echoed_when_provided_and_generated_when_absent()
    {
        (await Invoke("abc-123")).ShouldBe("abc-123");

        var generated = await Invoke(null);
        generated.ShouldNotBeNullOrWhiteSpace();
        (await Invoke(null)).ShouldNotBe(generated);
    }
}
