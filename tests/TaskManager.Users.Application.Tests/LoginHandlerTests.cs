using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Tests;

public class LoginHandlerTests : HandlerTestBase
{
    private static readonly User Ana = User.Rehydrate(
        Guid.NewGuid(), "Ana", Email.Create("ana@x.com").Value, "HASH", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    private LoginHandler Sut => new(Repo, Hasher, Tokens, Metrics);

    [Fact]
    public async Task HandleAsync_validCredentials_returnsTokenUsingNormalizedEmail()
    {
        Repo.GetByEmailAsync(Arg.Is<Email>(e => e.Value == "ana@x.com"), Arg.Any<CancellationToken>()).Returns(Ana);
        Hasher.Verify("HASH", "Secret123").Returns(true);
        var expires = Clock.UtcNow.AddMinutes(60);
        Tokens.Issue(Ana).Returns(new IssuedToken("jwt", expires));

        var result = await Sut.HandleAsync(new LoginCommand(" ANA@x.com ", "Secret123"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("jwt");
        result.Value.ExpiresAt.ShouldBe(expires);
        result.Value.User.Email.ShouldBe("ana@x.com");
    }

    [Fact]
    public async Task HandleAsync_blankFields_returnsValidationErrorWithoutLookup()
    {
        var result = await Sut.HandleAsync(new LoginCommand(null, ""), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors!.Keys.ShouldBe(["email", "password"], ignoreOrder: true);
        await Repo.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!, default);
        Metrics.DidNotReceiveWithAnyArgs().Increment(default!);
    }

    [Fact]
    public async Task HandleAsync_passwordOver128Characters_returnsInvalidCredentialsWithoutHashing()
    {
        var tooLong = new string('a', 128) + "1";

        var result = await Sut.HandleAsync(new LoginCommand("ana@x.com", tooLong), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        Hasher.DidNotReceiveWithAnyArgs().Verify(default, default!);
        await Repo.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!, default);
        Metrics.Received(1).Increment(UsersMetrics.LoginFailed);
    }

    [Fact]
    public async Task HandleAsync_wrongPassword_returnsInvalidCredentials()
    {
        Repo.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(Ana);
        Hasher.Verify("HASH", "Wrong123").Returns(false);

        var result = await Sut.HandleAsync(new LoginCommand("ana@x.com", "Wrong123"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        Tokens.DidNotReceiveWithAnyArgs().Issue(default!);
    }

    [Fact]
    public async Task HandleAsync_unknownEmail_returnsInvalidCredentialsAndStillVerifiesDummyHash()
    {
        Repo.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await Sut.HandleAsync(new LoginCommand("who@x.com", "Secret123"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        Hasher.Received(1).Verify(null, "Secret123");
    }

    [Fact]
    public async Task HandleAsync_malformedEmail_returnsInvalidCredentialsNotValidation()
    {
        var result = await Sut.HandleAsync(new LoginCommand("not-an-email", "Secret123"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        result.Error!.Type.ShouldNotBe(ErrorType.Validation);
    }

    [Fact]
    public async Task HandleAsync_failureThenSuccess_incrementsLoginFailedThenLoginSucceeded()
    {
        Repo.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(Ana);
        Hasher.Verify("HASH", "Bad12345").Returns(false);
        Hasher.Verify("HASH", "Secret123").Returns(true);
        Tokens.Issue(Ana).Returns(new IssuedToken("jwt", Clock.UtcNow));

        await Sut.HandleAsync(new LoginCommand("ana@x.com", "Bad12345"), CancellationToken.None);
        Metrics.Received(1).Increment(UsersMetrics.LoginFailed);

        await Sut.HandleAsync(new LoginCommand("ana@x.com", "Secret123"), CancellationToken.None);
        Metrics.Received(1).Increment(UsersMetrics.LoginSucceeded);
    }
}
