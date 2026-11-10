using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Testing;
using TaskManager.Users.Application.Ports;

namespace TaskManager.Users.Application.Tests;

public abstract class UserServiceTestBase
{
    protected readonly IUserRepository Repo = Substitute.For<IUserRepository>();
    protected readonly IPasswordHasher Hasher = Substitute.For<IPasswordHasher>();
    protected readonly ITokenIssuer Tokens = Substitute.For<ITokenIssuer>();
    protected readonly IAppMetrics Metrics = Substitute.For<IAppMetrics>();
    protected readonly FixedClock Clock = new(DateTimeOffset.Parse("2026-11-10T12:00:00Z"));

    protected UserService Sut => new(Repo, Hasher, Tokens, Clock, Metrics);
}
