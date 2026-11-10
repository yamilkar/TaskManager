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
            // The demo password is public (README), so never create it in a deployed environment.
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"{UserSeedOptions.SectionName}:Enabled is only allowed in Development; the demo credentials are public.");
            }

            await seeder.SeedAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
