using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Register;

namespace TaskManager.Users.Api.Tests;

/// <summary>
/// Without a client IP the per-IP limiter cannot tell callers apart. Sharing one bucket would let five requests
/// lock everyone out, so the auth endpoints fail closed with 503 and never reach a handler.
/// </summary>
public sealed class MissingClientIpTests
{
    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/register")]
    public async Task Auth_withoutClientIp_returns503AndSkipsHandlerEvenAfterManyRequests(string url)
    {
        using var factory = new UsersApiFactory(withoutRemoteIp: true);
        var client = factory.CreateClient();
        var body = new { fullName = "Ana", email = "ana@x.com", password = "Passw0rd1" };

        for (var i = 0; i < 7; i++)
        {
            var response = await client.PostAsJsonAsync(url, body);

            // Never 429: missing-IP requests do not share (or consume) a rate-limit bucket.
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("code").GetString().ShouldBe("auth.client_ip_unavailable");
        }

        await factory.Login.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
        await factory.Register.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
    }
}
