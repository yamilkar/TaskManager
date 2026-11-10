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

    [Fact]
    public void AddUsersInfrastructure_doesNotRegisterJwtOptions_becauseApiCommonOwnsThem()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var services = new ServiceCollection().AddUsersInfrastructure(config);

        services.ShouldNotContain(d =>
            d.ServiceType.IsGenericType &&
            d.ServiceType.GetGenericArguments().Contains(typeof(TaskManager.SharedKernel.Security.JwtOptions)));
    }

    [Fact]
    public void AddUsersInfrastructure_passwordHasher_isBuiltAtStartupNotOnFirstRequest()
    {
        // The hasher pre-computes its dummy hash in the constructor; registering a ready instance means
        // that cost is paid at startup, so the first unknown-email login is not measurably slower.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var descriptor = new ServiceCollection().AddUsersInfrastructure(config)
            .Single(d => d.ServiceType == typeof(IPasswordHasher));

        descriptor.ImplementationInstance.ShouldBeOfType<Security.AspNetPasswordHasher>();
    }
}
