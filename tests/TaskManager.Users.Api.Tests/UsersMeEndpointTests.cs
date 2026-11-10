using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Testing;
using TaskManager.Users.Application;
using TaskManager.Users.Application.GetCurrentUser;

namespace TaskManager.Users.Api.Tests;

public sealed class UsersMeEndpointTests
{
    private static HttpClient ClientWith(UsersApiFactory factory, string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        using var factory = new UsersApiFactory();

        var response = await ClientWith(factory, null).GetAsync("/api/v1/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_expired_token_returns_401()
    {
        using var factory = new UsersApiFactory();
        var token = TestJwt.Create(Guid.NewGuid(), factory.SigningKey, DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await ClientWith(factory, token).GetAsync("/api/v1/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_wrong_signature_returns_401()
    {
        using var factory = new UsersApiFactory();
        var token = TestJwt.Create(Guid.NewGuid(), "a-completely-different-signing-key-0123456789");

        var response = await ClientWith(factory, token).GetAsync("/api/v1/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_valid_token_queries_with_sub_and_returns_200()
    {
        using var factory = new UsersApiFactory();
        var id = Guid.NewGuid();
        factory.Me.HandleAsync(new GetCurrentUserQuery(id), Arg.Any<CancellationToken>())
            .Returns(Result<UserResponse>.Success(new UserResponse(id, "Ana", "ana@x.com", DateTimeOffset.UtcNow)));
        var token = TestJwt.Create(id, factory.SigningKey);

        var response = await ClientWith(factory, token).GetAsync("/api/v1/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().ShouldBe(id);
        await factory.Me.Received(1).HandleAsync(new GetCurrentUserQuery(id), Arg.Any<CancellationToken>());
    }
}
