using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TaskManager.Users.Application;
using TaskManager.Users.Infrastructure;

namespace TaskManager.Users.Api.Tests;

public sealed class UsersApiFactory(string signingKey = UsersApiFactory.DefaultKey) : WebApplicationFactory<Program>
{
    public const string DefaultKey = "test-signing-key-0123456789-abcdefghijklmnop";

    public string SigningKey { get; } = signingKey;

    public IUserService Users { get; } = Substitute.For<IUserService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = SigningKey }));

        builder.ConfigureServices(services =>
        {
            Replace<IUserService>(services, Users);
            Replace<IAmazonDynamoDB>(services, Substitute.For<IAmazonDynamoDB>());

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
