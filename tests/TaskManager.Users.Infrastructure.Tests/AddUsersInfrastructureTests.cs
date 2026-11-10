using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Infrastructure.Seeding;

namespace TaskManager.Users.Infrastructure.Tests;

public sealed class AddUsersInfrastructureTests
{
    [Fact]
    public void Services_resolve_with_in_memory_configuration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dynamo:ServiceUrl"] = "http://localhost:8000",
            ["Dynamo:TableName"] = "TaskManager-Users",
            ["Jwt:SigningKey"] = "unit-test-signing-key-with-at-least-32-bytes!",
        }).Build();

        using var provider = new ServiceCollection().AddUsersInfrastructure(config).BuildServiceProvider();

        provider.GetRequiredService<IUserRepository>().ShouldNotBeNull();
        provider.GetRequiredService<IPasswordHasher>().ShouldNotBeNull();
        provider.GetRequiredService<ITokenIssuer>().ShouldNotBeNull();
        provider.GetRequiredService<UserSeeder>().ShouldNotBeNull();
    }
}
