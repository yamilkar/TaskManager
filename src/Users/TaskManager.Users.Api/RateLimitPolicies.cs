using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TaskManager.Users.Api;

/// <summary>
/// Per-IP fixed-window limits for the anonymous auth endpoints. Login and register have separate buckets,
/// so registering does not use up sign-in attempts. Limits are in-memory per instance; see spec section 11
/// for the deployment notes (API Gateway throttling or WAF, and forwarded headers behind CloudFront).
/// </summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Register = "register";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var loginLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
        var registerLimit = configuration.GetValue("RateLimiting:RegisterPermitLimit", 5);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Headers.RetryAfter = "60";
                var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                var details = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = policy == Register
                        ? "Too many registration attempts. Please wait a minute and try again."
                        : "Too many sign-in attempts. Please wait a minute and try again.",
                };
                details.Extensions["code"] = "auth.rate_limited";
                await response.WriteAsJsonAsync(details, options: null, contentType: "application/problem+json", ct);
            };
            options.AddPolicy(Login, context => PerIpPerMinute(context, loginLimit));
            options.AddPolicy(Register, context => PerIpPerMinute(context, registerLimit));
        });
    }

    // A request without a client IP is not counted here: one shared bucket would let anyone lock everyone out.
    // RequireClientIpAttribute rejects such requests with 503 before they reach a handler.
    private static RateLimitPartition<string> PerIpPerMinute(HttpContext context, int permitLimit) =>
        context.Connection.RemoteIpAddress is not { } ip
            ? RateLimitPartition.GetNoLimiter("no-client-ip")
            : RateLimitPartition.GetFixedWindowLimiter(
            ip.ToString(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
}
