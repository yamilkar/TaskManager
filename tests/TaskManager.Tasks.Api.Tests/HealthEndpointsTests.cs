using System.Net;

namespace TaskManager.Tasks.Api.Tests;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Health_returns_200_without_auth()
    {
        using var factory = new TasksApiFactory();

        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ready_returns_200_when_table_reachable()
    {
        using var factory = new TasksApiFactory();

        var response = await factory.CreateClient().GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void Startup_fails_when_signing_key_missing()
    {
        using var factory = new TasksApiFactory(signingKey: "");

        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient())
            .Message.ShouldContain("Jwt:SigningKey");
    }
}
