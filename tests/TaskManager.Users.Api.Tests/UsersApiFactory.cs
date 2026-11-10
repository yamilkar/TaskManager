using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Users.Application;
using TaskManager.Users.Application.GetCurrentUser;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Application.Register;
using TaskManager.Users.Infrastructure;

namespace TaskManager.Users.Api.Tests;

/// <summary>
/// Hosts the Users API in the Testing environment. By default every handler is a substitute;
/// with <c>useRealHandlers</c> the real handlers run on top of a substituted <see cref="IUserRepository"/>.
/// </summary>
public sealed class UsersApiFactory(string signingKey = UsersApiFactory.DefaultKey, bool useRealHandlers = false)
    : WebApplicationFactory<Program>
{
    public const string DefaultKey = "test-signing-key-0123456789-abcdefghijklmnop";

    public string SigningKey { get; } = signingKey;

    public ICommandHandler<RegisterUserCommand, UserResponse> Register { get; } =
        Substitute.For<ICommandHandler<RegisterUserCommand, UserResponse>>();

    public ICommandHandler<LoginCommand, AuthResponse> Login { get; } =
        Substitute.For<ICommandHandler<LoginCommand, AuthResponse>>();

    public IQueryHandler<GetCurrentUserQuery, UserResponse> Me { get; } =
        Substitute.For<IQueryHandler<GetCurrentUserQuery, UserResponse>>();

    public IUserRepository Repository { get; } = Substitute.For<IUserRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = SigningKey }));

        builder.ConfigureServices(services =>
        {
            if (!useRealHandlers)
            {
                Replace(services, Register);
                Replace(services, Login);
                Replace(services, Me);
            }

            Replace(services, Repository);
            Replace(services, Substitute.For<IAmazonDynamoDB>());

            var bootstrappers = services.Where(d =>
                d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(UsersDatabaseBootstrapper)).ToList();
            foreach (var descriptor in bootstrappers)
            {
                services.Remove(descriptor);
            }
        });
    }

    private static void Replace<T>(IServiceCollection services, T instance) where T : class
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(instance);
    }
}
