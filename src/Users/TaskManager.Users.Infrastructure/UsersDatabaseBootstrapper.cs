using Amazon.DynamoDBv2;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
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
            // The default demo password is public (README), so a deployed environment must bring its own, private one.
            var password = seedOptions.Value.DemoPassword;
            if (!environment.IsDevelopment() &&
                (string.IsNullOrWhiteSpace(password) || password == UserSeedOptions.PublicDemoPassword))
            {
                throw new InvalidOperationException(
                    $"{UserSeedOptions.SectionName}:DemoPassword must be set to a private value outside Development; the default demo password is public.");
            }

            await seeder.SeedAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
