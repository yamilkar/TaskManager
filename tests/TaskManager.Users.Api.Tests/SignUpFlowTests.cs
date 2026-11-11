using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Api.Tests;

/// <summary>Real handlers, hasher and token issuer over an in-memory user store keyed by normalized email.</summary>
public sealed class SignUpFlowTests
{
    private readonly ConcurrentDictionary<string, User> _store = new();

    private UsersApiFactory CreateFactory()
    {
        var factory = new UsersApiFactory(useRealHandlers: true);
        factory.Repository.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var user = call.Arg<User>();
                return _store.TryAdd(user.Email.Value, user) ? AddUserResult.Added : AddUserResult.EmailTaken;
            });
        factory.Repository.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(call => _store.GetValueOrDefault(call.Arg<Email>().Value));
        return factory;
    }

    [Fact]
    public async Task RegisterThenLogin_newAccount_returnsTokenForTheNewUser()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Ana Lopez", email = "ana@x.com", password = "Passw0rd!" });
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var userId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana@x.com", password = "Passw0rd!" });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        new JsonWebTokenHandler().ReadJsonWebToken(token).Subject.ShouldBe(userId);
    }

    [Fact]
    public async Task Register_sameEmailDifferentCaseAndSpaces_returns409AndStoresOneUser()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Ana Lopez", email = "ana@x.com", password = "Passw0rd!" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Ana Again", email = " Ana@X.COM ", password = "Passw0rd!" });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .ShouldBe("users.email_taken");
        _store.Count.ShouldBe(1);
    }
}
