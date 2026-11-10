using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Users.Application;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Register;

namespace TaskManager.Users.Api.Tests;

public sealed class AuthEndpointsTests
{
    private static readonly UserResponse Ana = new(Guid.NewGuid(), "Ana", "ana@x.com", DateTimeOffset.UtcNow);

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Register_returns_201_with_location_and_body()
    {
        using var factory = new UsersApiFactory();
        factory.Register.HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserResponse>.Success(Ana));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Ana", email = "ana@x.com", password = "Passw0rd1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldEndWith("/api/v1/users/me");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().ShouldBe("ana@x.com");
        body.GetProperty("fullName").GetString().ShouldBe("Ana");
    }

    [Fact]
    public async Task Register_maps_request_body_to_command()
    {
        using var factory = new UsersApiFactory();
        factory.Register.HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserResponse>.Success(Ana));

        await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Ana", email = "ana@x.com", password = "Passw0rd1" });

        await factory.Register.Received(1).HandleAsync(
            new RegisterUserCommand("Ana", "ana@x.com", "Passw0rd1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_maps_request_body_to_command()
    {
        using var factory = new UsersApiFactory();
        factory.Login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthResponse>.Failure(UserErrors.InvalidCredentials));

        await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "ana@x.com", password = "pw" });

        await factory.Login.Received(1).HandleAsync(new LoginCommand("ana@x.com", "pw"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_sixth_request_within_a_minute_returns_429()
    {
        using var factory = new UsersApiFactory();
        factory.Register.HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserResponse>.Failure(UserErrors.EmailAlreadyRegistered));
        var client = factory.CreateClient();
        var request = new { fullName = "Ana", email = "ana@x.com", password = "Passw0rd1" };

        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/auth/register", request)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var body = await limited.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("auth.rate_limited");
        await factory.Register.Received(5).HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_requests_do_not_consume_login_permits()
    {
        using var factory = new UsersApiFactory();
        factory.Register.HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserResponse>.Failure(UserErrors.EmailAlreadyRegistered));
        factory.Login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthResponse>.Failure(UserErrors.InvalidCredentials));
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "Ana", email = "ana@x.com", password = "Passw0rd1" });
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana@x.com", password = "x" });

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_returns_409_problem_with_code_when_email_taken()
    {
        using var factory = new UsersApiFactory();
        factory.Register.HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserResponse>.Failure(UserErrors.EmailAlreadyRegistered));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Ana", email = "ana@x.com", password = "Passw0rd1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("users.email_taken");
    }

    [Fact]
    public async Task Register_returns_400_validation_problem_with_field_errors()
    {
        using var factory = new UsersApiFactory();
        var error = Error.Validation("validation_failed", "bad", new Dictionary<string, string[]> { ["email"] = ["Email is invalid."] });
        factory.Register.HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserResponse>.Failure(error));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Ana", email = "nope", password = "Passw0rd1" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe("Email is invalid.");
    }

    [Fact]
    public async Task Register_with_malformed_json_returns_400_problem_not_500()
    {
        using var factory = new UsersApiFactory();

        var response = await factory.CreateClient().PostAsync("/api/v1/auth/register", Json("{ not json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Register_with_empty_body_returns_400()
    {
        using var factory = new UsersApiFactory();

        var response = await factory.CreateClient().PostAsync("/api/v1/auth/register", Json(""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_returns_200_with_token()
    {
        using var factory = new UsersApiFactory();
        factory.Login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthResponse>.Success(new AuthResponse("tok", DateTimeOffset.UtcNow.AddHours(1), Ana)));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "ana@x.com", password = "Passw0rd1" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().ShouldBe("tok");
    }

    [Fact]
    public async Task Login_returns_401_problem_with_message_Invalid_email_or_password()
    {
        using var factory = new UsersApiFactory();
        factory.Login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthResponse>.Failure(UserErrors.InvalidCredentials));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "ana@x.com", password = "wrong" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task Login_with_empty_fields_returns_400_validation_problem()
    {
        // Real handlers over a substituted repository: validation lives in the login handler,
        // so this checks the handler-error to HTTP mapping end to end.
        using var factory = new UsersApiFactory(useRealHandlers: true);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();
        body.GetProperty("errors").TryGetProperty("password", out _).ShouldBeTrue();
        await factory.Repository.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!, default);
    }

    [Fact]
    public async Task Login_with_malformed_email_returns_401()
    {
        using var factory = new UsersApiFactory();
        factory.Login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthResponse>.Failure(UserErrors.InvalidCredentials));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "not-an-email", password = "Passw0rd1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_sixth_request_within_a_minute_returns_429()
    {
        using var factory = new UsersApiFactory();
        factory.Login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthResponse>.Failure(UserErrors.InvalidCredentials));
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana@x.com", password = "x" });
            ok.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ana@x.com", password = "x" });

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        limited.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(60));
        var body = await limited.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("status").GetInt32().ShouldBe(429);
        body.GetProperty("title").GetString().ShouldBe("Too many requests");
        body.GetProperty("detail").GetString().ShouldBe("Too many sign-in attempts. Please wait a minute and try again.");
        body.GetProperty("code").GetString().ShouldBe("auth.rate_limited");
    }
}
