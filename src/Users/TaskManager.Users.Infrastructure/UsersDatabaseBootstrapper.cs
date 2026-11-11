using Amazon.DynamoDBv2;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Users.Application.Register;
using TaskManager.Users.Infrastructure.Persistence;
using TaskManager.Users.Infrastructure.Seeding;

namespace TaskManager.Users.Infrastructure;

public sealed class UsersDatabaseBootstrapper(
    IAmazonDynamoDB db,
    IOptions<DynamoDbOptions> dynamoOptions,
    IOptions<UserSeedOptions> seedOptions,
    UserSeeder seeder,
    IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (dynamoOptions.Value.CreateTablesIfMissing)
        {
            await TableBootstrapper.EnsureTableAsync(
                db, UsersTableDefinition.Create(dynamoOptions.Value.TableName), cancellationToken);
        }

        if (seedOptions.Value.Enabled)
        {
            if (!environment.IsDevelopment())
            {
                EnsureDeployableDemoPassword(seedOptions.Value.DemoPassword);
            }

            await seeder.SeedAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Outside Development the demo user is reachable from the internet: its password must be private (not the
    /// public README default) and meet the same rule as any registered account. Messages never include the value.
    /// </summary>
    private static void EnsureDeployableDemoPassword(string? password)
    {
        const string Key = $"{UserSeedOptions.SectionName}:DemoPassword";

        if (string.IsNullOrWhiteSpace(password) || password == UserSeedOptions.PublicDemoPassword)
        {
            throw new InvalidOperationException(
                $"{Key} must be set to a private value outside Development; the default demo password is public.");
        }

        var problems = RegisterUserValidator.PasswordProblems(password);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException($"{Key} is too weak: {string.Join(" ", problems)}");
        }
    }
}
