using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace TaskManager.ApiCommon;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 100;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsAcceptable(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Reject oversized or control-character values so they cannot forge log lines or headers.
    private static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(c => !char.IsControl(c));
}
