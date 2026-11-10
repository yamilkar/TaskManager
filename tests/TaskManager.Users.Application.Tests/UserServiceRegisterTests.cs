using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Users.Application.Contracts;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Tests;

public class UserServiceRegisterTests : UserServiceTestBase
{
    private static readonly RegisterUserRequest Valid = new(" Ana ", " Ana@X.com ", "Secret123");

    [Fact]
    public async Task RegisterAsync_hashes_password_and_returns_user_with_normalized_email()
    {
        Hasher.Hash("Secret123").Returns("HASHED");
        Repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Sut.RegisterAsync(Valid, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("ana@x.com");
        result.Value.FullName.ShouldBe("Ana");
        result.Value.CreatedAt.ShouldBe(Clock.UtcNow);
        await Repo.Received(1).TryAddAsync(
            Arg.Is<User>(u => u.Email.Value == "ana@x.com" && u.PasswordHash == "HASHED"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_returns_conflict_when_repository_reports_existing_email()
    {
        Hasher.Hash(Arg.Any<string>()).Returns("HASHED");
        Repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Sut.RegisterAsync(Valid, CancellationToken.None);

        result.Error.ShouldBe(UserErrors.EmailAlreadyRegistered);
        Metrics.DidNotReceiveWithAnyArgs().Increment(default!);
    }

    [Fact]
    public async Task RegisterAsync_does_not_touch_repository_when_invalid()
    {
        var result = await Sut.RegisterAsync(new RegisterUserRequest("", "bad", "x"), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        await Repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }

    [Fact]
    public async Task RegisterAsync_increments_UserRegistered_metric_on_success()
    {
        Hasher.Hash(Arg.Any<string>()).Returns("HASHED");
        Repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(true);

        await Sut.RegisterAsync(Valid, CancellationToken.None);

        Metrics.Received(1).Increment(UsersMetrics.UserRegistered);
    }
}
