using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaskManager.Users.Api;

/// <summary>
/// Fails closed when the connection has no client IP (a hosting or proxy misconfiguration). The per-IP rate
/// limits cannot work without it, so the request gets 503 and a warning is logged instead of reaching a handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireClientIpAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Connection.RemoteIpAddress is not null)
        {
            return;
        }

        context.HttpContext.RequestServices.GetRequiredService<ILogger<RequireClientIpAttribute>>()
            .LogWarning("Rejected {Path}: the request has no client IP, so per-IP rate limiting cannot apply.",
                context.HttpContext.Request.Path);

        var details = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Service Unavailable",
            Detail = "The client address could not be determined. Please try again later.",
        };
        details.Extensions["code"] = "auth.client_ip_unavailable";
        context.Result = new ObjectResult(details)
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable,
            ContentTypes = { "application/problem+json" },
        };
    }
}
