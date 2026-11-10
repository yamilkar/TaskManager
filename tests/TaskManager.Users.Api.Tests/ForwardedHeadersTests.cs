using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Users.Application;
using TaskManager.Users.Application.Login;

namespace TaskManager.Users.Api.Tests;

/// <summary>
/// The login limiter partitions on the client IP. Behind a proxy that IP comes from X-Forwarded-For, which is
/// trusted only when the TCP peer is a configured proxy; otherwise the header is ignored so clients can't spoof it.
/// </summary>
public sealed class ForwardedHeadersTests
{
    private static readonly IPAddress Proxy = IPAddress.Parse("10.0.0.5");

    private static UsersApiFactory Factory(IReadOnlyDictionary<string, string?>? settings)
    {
        var factory = new UsersApiFactory(remoteIp: Proxy, settings: settings);
        factory.Login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthResponse>.Failure(UserErrors.InvalidCredentials));
        return factory;
    }

    private static async Task<HttpStatusCode> LoginFrom(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "ana@x.com", password = "x" }),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "10.0.0.5")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8")]
    public async Task Login_fromTrustedProxy_rateLimitsEachForwardedClientSeparately(string key, string value)
    {
        using var factory = Factory(new Dictionary<string, string?> { [key] = value });
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await LoginFrom(client, "203.0.113.1")).ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await LoginFrom(client, "203.0.113.1")).ShouldBe(HttpStatusCode.TooManyRequests);
        (await LoginFrom(client, "203.0.113.2")).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_withoutTrustedProxyConfig_ignoresForwardedHeader()
    {
        using var factory = Factory(settings: null);
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await LoginFrom(client, $"203.0.113.{i + 1}")).ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Rotating the spoofed header does not escape the peer's bucket.
        (await LoginFrom(client, "203.0.113.99")).ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
