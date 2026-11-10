using NSubstitute;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Tests;

public class UserServiceGetByIdTests : UserServiceTestBase
{
    [Fact]
    public async Task GetByIdAsync_returns_NotFound_when_missing()
    {
        Repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await Sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.NotFound);
    }

    [Fact]
    public async Task GetByIdAsync_returns_user_when_found()
    {
        var id = Guid.NewGuid();
        var user = User.Rehydrate(id, "Ana", Email.Create("ana@x.com").Value, "H", Clock.UtcNow);
        Repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Sut.GetByIdAsync(id, CancellationToken.None);

        result.Value.Id.ShouldBe(id);
        result.Value.Email.ShouldBe("ana@x.com");
    }
}
