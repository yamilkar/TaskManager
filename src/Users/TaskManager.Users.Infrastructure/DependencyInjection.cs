using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.SharedKernel;
using TaskManager.SharedKernel.Security;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Infrastructure.Persistence;
using TaskManager.Users.Infrastructure.Security;
using TaskManager.Users.Infrastructure.Seeding;

namespace TaskManager.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDynamoDb(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options =>
            {
                options.EnsureValid();
                return true;
            })
            .ValidateOnStart();
        services.Configure<UserSeedOptions>(configuration.GetSection(UserSeedOptions.SectionName));

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUserRepository, DynamoDbUserRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<UserSeeder>();
        services.AddHostedService<UsersDatabaseBootstrapper>();

        return services;
    }
}
