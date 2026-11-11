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

    private const string PrivatePassword = "a-Private-demo-passw0rd";

    private UsersDatabaseBootstrapper Create(string environment, bool seedEnabled, string? demoPassword = null)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var seed = new UserSeedOptions { Enabled = seedEnabled };
        if (demoPassword is not null)
        {
            seed.DemoPassword = demoPassword;
        }

        var seedOptions = Options.Create(seed);
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
    [InlineData("Production", null)]
    [InlineData("Staging", null)]
    [InlineData("Production", UserSeedOptions.PublicDemoPassword)]
    [InlineData("Production", "")]
    [InlineData("Production", "   ")]
    public async Task StartAsync_seedEnabledOutsideDevelopmentWithPublicOrBlankPassword_failsWithoutCreatingDemoUser(
        string environment, string? demoPassword)
    {
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => Create(environment, seedEnabled: true, demoPassword).StartAsync(default));

        ex.Message.ShouldContain("Seed:DemoPassword");
        await _repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }

    [Fact]
    public async Task StartAsync_seedEnabledOutsideDevelopmentWithPrivatePassword_seedsDemoUser()
    {
        _repo.TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(AddUserResult.Added);

        await Create("Production", seedEnabled: true, PrivatePassword).StartAsync(default);

        await _repo.Received(1).TryAddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_seedDisabledInProduction_starts()
    {
        await Create("Production", seedEnabled: false).StartAsync(default);

        await _repo.DidNotReceiveWithAnyArgs().TryAddAsync(default!, default);
    }
}
