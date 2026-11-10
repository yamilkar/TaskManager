using NSubstitute;
using TaskManager.Users.Application.GetCurrentUser;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Tests;

public class GetCurrentUserHandlerTests : HandlerTestBase
{
    private GetCurrentUserHandler Sut => new(Repo);

    [Fact]
    public async Task HandleAsync_missingUser_returnsNotFound()
    {
        Repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await Sut.HandleAsync(new GetCurrentUserQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.NotFound);
    }

    [Fact]
    public async Task HandleAsync_existingUser_returnsUser()
    {
        var id = Guid.NewGuid();
        var user = User.Rehydrate(id, "Ana", Email.Create("ana@x.com").Value, "H", Clock.UtcNow);
        Repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await Sut.HandleAsync(new GetCurrentUserQuery(id), CancellationToken.None);

        result.Value.Id.ShouldBe(id);
        result.Value.Email.ShouldBe("ana@x.com");
    }
}
