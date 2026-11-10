using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManager.ApiCommon;
using TaskManager.SharedKernel;

namespace TaskManager.ApiCommon.Tests;

public sealed class ApiDefaultsExtensionsTests
{
    private static readonly string Key = new('k', 64);

    private static WebApplicationBuilder NewBuilder(string? key)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (key is not null)
        {
            builder.Configuration["Jwt:SigningKey"] = key;
        }

        return builder;
    }

    private static async Task<WebApplication> StartAsync(WebApplicationBuilder builder)
    {
        var app = builder.AddTaskManagerApiDefaults().Build();
        app.UseTaskManagerApiDefaults();
        app.MapGet("/boom", () => { throw new InvalidOperationException("kaboom"); });
        app.MapGet("/secure", () => "ok").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private static string Token(string algorithm)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "taskmanager-users",
            Audience = "taskmanager-api",
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), algorithm),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [Theory]
    [InlineData("short")]
    [InlineData(null)]
    public async Task Host_fails_to_start_when_signing_key_is_short_or_missing(string? key)
    {
        await using var app = NewBuilder(key).AddTaskManagerApiDefaults().Build();

        await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync());
    }

    [Fact]
    public async Task Builds_registers_metrics_and_honors_configuration_supplied_late()
    {
        var builder = NewBuilder(null).AddTaskManagerApiDefaults();
        builder.Configuration["Jwt:SigningKey"] = Key; // after AddTaskManagerApiDefaults, as WebApplicationFactory does

        await using var app = builder.Build();
        await app.StartAsync();

        app.Services.GetRequiredService<IAppMetrics>().ShouldBeOfType<EmfAppMetrics>();
        app.Services.GetRequiredService<IOptions<TaskManager.SharedKernel.Security.JwtOptions>>().Value.SigningKey.ShouldBe(Key);
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_problem_with_correlation_id_header()
    {
        await using var app = await StartAsync(NewBuilder(Key));
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/boom");
        request.Headers.Add("X-Correlation-Id", "corr-42");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.GetValues("X-Correlation-Id").ShouldBe(["corr-42"]);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("kaboom");
    }

    [Fact]
    public async Task HS256_token_is_accepted_and_HS512_token_with_same_key_is_rejected()
    {
        await using var app = await StartAsync(NewBuilder(Key));
        using var client = app.GetTestClient();

        async Task<HttpStatusCode> Call(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (await client.SendAsync(request)).StatusCode;
        }

        (await Call(Token(SecurityAlgorithms.HmacSha256))).ShouldBe(HttpStatusCode.OK);
        (await Call(Token(SecurityAlgorithms.HmacSha512))).ShouldBe(HttpStatusCode.Unauthorized);
    }
}
