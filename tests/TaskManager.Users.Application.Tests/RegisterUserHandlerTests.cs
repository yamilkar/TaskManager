using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Users.Application.Register;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Tests;

public class RegisterUserHandlerTests : HandlerTestBase
{
    private static readonly RegisterUserCommand Valid = new(" Ana ", " Ana@X.com ", "Secret123");

    private RegisterUserHandler Sut => new(Repo, Hasher, Clock, Metrics);

    [Fact]
    public async Task HandleAsync_validCommand_hashesPasswordAndReturnsNormalizedUser()
    {
        Hasher.Hash("Secret123").Returns("HASHED");
        Repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Sut.HandleAsync(Valid, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("ana@x.com");
        result.Value.FullName.ShouldBe("Ana");
        result.Value.CreatedAt.ShouldBe(Clock.UtcNow);
        await Repo.Received(1).TryAddAsync(
            Arg.Is<User>(u => u.Email.Value == "ana@x.com" && u.PasswordHash == "HASHED"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_emailAlreadyRegistered_returnsConflict()
    {
        Hasher.Hash(Arg.Any<string>()).Returns("HASHED");
        Repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Sut.HandleAsync(Valid, CancellationToken.None);

        result.Error.ShouldBe(UserErrors.EmailAlreadyRegistered);
        Metrics.DidNotReceiveWithAnyArgs().Increment(default!);
    }

    [Fact]
    public async Task HandleAsync_invalidCommand_returnsValidationErrorWithoutTouchingRepository()
    {
        var result = await Sut.HandleAsync(new RegisterUserCommand("", "bad", "x"), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        await Repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_success_incrementsUserRegistered()
    {
        Hasher.Hash(Arg.Any<string>()).Returns("HASHED");
        Repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(true);

        await Sut.HandleAsync(Valid, CancellationToken.None);

        Metrics.Received(1).Increment(UsersMetrics.UserRegistered);
    }
}
