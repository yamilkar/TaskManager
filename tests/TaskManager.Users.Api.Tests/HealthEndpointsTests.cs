using System.Net;

namespace TaskManager.Users.Api.Tests;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Health_returns_200_without_auth()
    {
        using var factory = new UsersApiFactory();

        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ready_checks_dynamo_table_and_returns_200_when_reachable()
    {
        using var factory = new UsersApiFactory();

        var response = await factory.CreateClient().GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public void Startup_fails_when_signing_key_missing()
    {
        using var factory = new UsersApiFactory(signingKey: "");

        // Same failure as the Tasks API: JwtOptions are validated once, in ApiCommon.
        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient())
            .Message.ShouldContain("Jwt:SigningKey");
    }
}
