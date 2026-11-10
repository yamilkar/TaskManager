using Amazon.DynamoDBv2;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Testing;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;
using TaskManager.Users.Infrastructure.Seeding;

namespace TaskManager.Users.Infrastructure.Tests;

public sealed class UsersDatabaseBootstrapperTests
{
    private readonly IUserRepository _repo = Substitute.For<IUserRepository>();

    private UsersDatabaseBootstrapper Create(string environment, bool seedEnabled)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var seedOptions = Options.Create(new UserSeedOptions { Enabled = seedEnabled });
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns("HASH");
        var seeder = new UserSeeder(_repo, hasher, new FixedClock(DateTimeOffset.UtcNow), seedOptions);

        return new UsersDatabaseBootstrapper(
            Substitute.For<IAmazonDynamoDB>(),
            Options.Create(new DynamoDbOptions { TableName = "Users" }),
            seedOptions,
            seeder,
            env);
    }

    [Fact]
    public async Task StartAsync_seedEnabledInDevelopment_seedsDemoUser()
    {
        _repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(AddUserResult.Added);

        await Create(Environments.Development, seedEnabled: true).StartAsync(default);

        await _repo.Received(1).TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task StartAsync_seedEnabledOutsideDevelopment_failsWithoutCreatingDemoUser(string environment)
    {
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Create(environment, seedEnabled: true).StartAsync(default));

        ex.Message.ShouldContain("Seed:Enabled");
        await _repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }

    [Fact]
    public async Task StartAsync_seedDisabledInProduction_starts()
    {
        await Create("Production", seedEnabled: false).StartAsync(default);

        await _repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }
}
