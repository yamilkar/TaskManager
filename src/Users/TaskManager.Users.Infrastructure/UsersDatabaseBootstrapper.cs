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
    UserSeeder seeder) : IHostedService
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
            await seeder.SeedAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
