using Amazon.DynamoDBv2;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Infrastructure.Seeding;

namespace TaskManager.Tasks.Infrastructure;

public sealed class TasksDatabaseBootstrapper(
    IAmazonDynamoDB db,
    IOptions<DynamoDbOptions> dynamoOptions,
    IOptions<TaskSeedOptions> seedOptions,
    TaskSeeder seeder) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (dynamoOptions.Value.CreateTablesIfMissing)
        {
            await TableBootstrapper.EnsureTableAsync(
                db, TasksTableDefinition.Create(dynamoOptions.Value.TableName), cancellationToken);
        }

        if (seedOptions.Value.Enabled)
        {
            await seeder.SeedAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
