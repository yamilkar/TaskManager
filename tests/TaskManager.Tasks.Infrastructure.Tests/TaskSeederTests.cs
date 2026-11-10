using Amazon.DynamoDBv2;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Tasks.Domain;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Infrastructure.Seeding;
using TaskManager.Testing;

namespace TaskManager.Tasks.Infrastructure.Tests;

[Collection("dynamodb")]
public sealed class TaskSeederTests : IAsyncLifetime
{
    private static readonly Guid DemoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 23, 30, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _db;
    private readonly string _table = $"Tasks-{Guid.NewGuid():N}";
    private readonly DynamoDbTaskRepository _repo;
    private readonly TaskSeeder _seeder;

    public TaskSeederTests(DynamoDbLocalFixture fixture)
    {
        _db = fixture.Client;
        _repo = new DynamoDbTaskRepository(_db, Options.Create(new DynamoDbOptions { TableName = _table }));
        _seeder = new TaskSeeder(_repo, new FixedClock(Now), Options.Create(new TaskSeedOptions()));
    }

    public Task InitializeAsync() => TableBootstrapper.EnsureTableAsync(_db, TasksTableDefinition.Create(_table), CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedAsync_adds_four_tasks_once()
    {
        await _seeder.SeedAsync(default);
        await _seeder.SeedAsync(default);

        var tasks = (await _repo.ListByUserAsync(DemoId, 100, null, default)).Items;
        tasks.Count.ShouldBe(4);

        var byTitle = tasks.ToDictionary(t => t.Title);
        byTitle["Explore the Task Manager"].Status.ShouldBe(TaskItemStatus.InProgress);
        byTitle["Explore the Task Manager"].DueDate.ShouldBe(new DateOnly(2026, 11, 13));
        byTitle["Review sprint backlog"].Status.ShouldBe(TaskItemStatus.ToDo);
        byTitle["Review sprint backlog"].DueDate.ShouldBe(new DateOnly(2026, 11, 17));
        byTitle["Set up local environment"].Status.ShouldBe(TaskItemStatus.Done);
        byTitle["Set up local environment"].DueDate.ShouldBeNull();
        byTitle["Write project notes"].Status.ShouldBe(TaskItemStatus.ToDo);
        byTitle["Write project notes"].DueDate.ShouldBeNull();
    }
}
