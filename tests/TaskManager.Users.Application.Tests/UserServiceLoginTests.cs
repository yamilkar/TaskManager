using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Users.Application.Contracts;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Tests;

public class UserServiceLoginTests : UserServiceTestBase
{
    private static readonly User Ana = User.Rehydrate(
        Guid.NewGuid(), "Ana", Email.Create("ana@x.com").Value, "HASH", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    [Fact]
    public async Task LoginAsync_returns_token_for_valid_credentials_using_normalized_email()
    {
        Repo.GetByEmailAsync(Arg.Is<Email>(e => e.Value == "ana@x.com"), Arg.Any<CancellationToken>()).Returns(Ana);
        Hasher.Verify("HASH", "Secret123").Returns(true);
        var expires = Clock.UtcNow.AddMinutes(60);
        Tokens.Issue(Ana).Returns(new IssuedToken("jwt", expires));

        var result = await Sut.LoginAsync(new LoginRequest(" ANA@x.com ", "Secret123"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("jwt");
        result.Value.ExpiresAt.ShouldBe(expires);
        result.Value.User.Email.ShouldBe("ana@x.com");
    }

    [Fact]
    public async Task LoginAsync_returns_InvalidCredentials_for_wrong_password()
    {
        Repo.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(Ana);
        Hasher.Verify("HASH", "Wrong123").Returns(false);

        var result = await Sut.LoginAsync(new LoginRequest("ana@x.com", "Wrong123"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        Tokens.DidNotReceiveWithAnyArgs().Issue(default!);
    }

    [Fact]
    public async Task LoginAsync_returns_InvalidCredentials_for_unknown_email_and_still_verifies_dummy()
    {
        Repo.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await Sut.LoginAsync(new LoginRequest("who@x.com", "Secret123"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        Hasher.Received(1).Verify(null, "Secret123");
    }

    [Fact]
    public async Task LoginAsync_returns_InvalidCredentials_for_malformed_email()
    {
        var result = await Sut.LoginAsync(new LoginRequest("not-an-email", "Secret123"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        result.Error!.Type.ShouldNotBe(ErrorType.Validation);
    }

    [Fact]
    public async Task LoginAsync_increments_LoginFailed_on_failure_and_LoginSucceeded_on_success()
    {
        Repo.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(Ana);
        Hasher.Verify("HASH", "Bad12345").Returns(false);
        Hasher.Verify("HASH", "Secret123").Returns(true);
        Tokens.Issue(Ana).Returns(new IssuedToken("jwt", Clock.UtcNow));

        await Sut.LoginAsync(new LoginRequest("ana@x.com", "Bad12345"), CancellationToken.None);
        Metrics.Received(1).Increment(UsersMetrics.LoginFailed);

        await Sut.LoginAsync(new LoginRequest("ana@x.com", "Secret123"), CancellationToken.None);
        Metrics.Received(1).Increment(UsersMetrics.LoginSucceeded);
    }
}
