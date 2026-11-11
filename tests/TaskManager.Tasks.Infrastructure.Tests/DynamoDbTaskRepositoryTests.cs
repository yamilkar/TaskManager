using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Tasks.Domain;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Testing;

namespace TaskManager.Tasks.Infrastructure.Tests;

[CollectionDefinition("dynamodb")]
public sealed class DynamoDbCollection : ICollectionFixture<DynamoDbLocalFixture>;

[Collection("dynamodb")]
public sealed class DynamoDbTaskRepositoryTests : IAsyncLifetime
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _table = $"Tasks-{Guid.NewGuid():N}";
    private readonly DynamoDbTaskRepository _repo;

    public DynamoDbTaskRepositoryTests(DynamoDbLocalFixture fixture)
    {
        _db = fixture.Client;
        _repo = new DynamoDbTaskRepository(_db, Options.Create(new DynamoDbOptions { TableName = _table }));
    }

    public Task InitializeAsync() => TableBootstrapper.EnsureTableAsync(_db, TasksTableDefinition.Create(_table), CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    // Rehydrate bypasses the domain's 2000-char cap so the paging test can build >1 MB of data.
    private static TaskItem NewTask(Guid userId, string? description = null) =>
        TaskItem.Rehydrate(Guid.NewGuid(), userId, "Task", description, TaskItemStatus.ToDo, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddAsync_then_GetAsync_returns_task()
    {
        var task = NewTask(Guid.NewGuid(), "d");
        await _repo.AddAsync(task, default);

        var found = await _repo.GetAsync(task.UserId, task.Id, default);
        found.ShouldNotBeNull();
        found.Id.ShouldBe(task.Id);
        found.Description.ShouldBe("d");
    }

    [Fact]
    public async Task ListByUserAsync_returns_only_that_users_tasks()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await _repo.AddAsync(NewTask(a), default);
        await _repo.AddAsync(NewTask(a), default);
        await _repo.AddAsync(NewTask(b), default);

        var list = await _repo.ListByUserAsync(a, default);

        list.Count.ShouldBe(2);
        list.ShouldAllBe(t => t.UserId == a);
    }

    [Fact]
    public async Task GetAsync_with_other_users_id_returns_null()
    {
        var task = NewTask(Guid.NewGuid());
        await _repo.AddAsync(task, default);

        (await _repo.GetAsync(Guid.NewGuid(), task.Id, default)).ShouldBeNull();
    }

    [Fact]
    public async Task ListByUserAsync_pages_through_more_than_one_page()
    {
        var user = Guid.NewGuid();
        var big = new string('x', 40_000);
        for (var i = 0; i < 30; i++)
        {
            await _repo.AddAsync(NewTask(user, big), default);
        }

        (await _repo.ListByUserAsync(user, default)).Count.ShouldBe(30);
    }

    [Fact]
    public async Task AddAsync_rejects_duplicate_id()
    {
        var task = NewTask(Guid.NewGuid());
        await _repo.AddAsync(task, default);

        await Should.ThrowAsync<ConditionalCheckFailedException>(() => _repo.AddAsync(task, default));
    }
    private static TaskItem Changed(TaskItem t, Guid? userId = null, string title = "Changed", string? description = null, DateOnly? due = null) =>
        TaskItem.Rehydrate(t.Id, userId ?? t.UserId, title, description, TaskItemStatus.Done, due, t.CreatedAt, t.CreatedAt.AddHours(1));

    [Fact]
    public async Task UpdateAsync_existingTask_replacesFieldsAndReturnsTrue()
    {
        var task = NewTask(Guid.NewGuid());
        await _repo.AddAsync(task, default);

        var replaced = await _repo.UpdateAsync(Changed(task, due: new DateOnly(2026, 12, 1)), default);

        replaced.ShouldBeTrue();
        var found = await _repo.GetAsync(task.UserId, task.Id, default);
        found.ShouldNotBeNull();
        found.Title.ShouldBe("Changed");
        found.Status.ShouldBe(TaskItemStatus.Done);
        found.DueDate.ShouldBe(new DateOnly(2026, 12, 1));
        found.CreatedAt.ShouldBe(task.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_clearedDescriptionAndDueDate_removesThemFromItem()
    {
        var task = TaskItem.Rehydrate(Guid.NewGuid(), Guid.NewGuid(), "Task", "d", TaskItemStatus.ToDo, new DateOnly(2026, 12, 1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await _repo.AddAsync(task, default);

        (await _repo.UpdateAsync(Changed(task), default)).ShouldBeTrue();

        var found = await _repo.GetAsync(task.UserId, task.Id, default);
        found!.Description.ShouldBeNull();
        found.DueDate.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_missingTask_returnsFalseAndCreatesNothing()
    {
        var task = NewTask(Guid.NewGuid());

        (await _repo.UpdateAsync(task, default)).ShouldBeFalse();

        (await _repo.GetAsync(task.UserId, task.Id, default)).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_sameIdUnderOtherUser_returnsFalseAndLeavesOwnerTaskUnchanged()
    {
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var task = NewTask(owner);
        await _repo.AddAsync(task, default);

        (await _repo.UpdateAsync(Changed(task, userId: intruder), default)).ShouldBeFalse();

        (await _repo.GetAsync(owner, task.Id, default))!.Title.ShouldBe("Task");
        (await _repo.GetAsync(intruder, task.Id, default)).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_twoUpdatesFromSameVersion_lastWriteWins()
    {
        // Spec D26: no optimistic concurrency. Both writers read the same version; both succeed and the second wins.
        var task = NewTask(Guid.NewGuid());
        await _repo.AddAsync(task, default);

        (await _repo.UpdateAsync(Changed(task, title: "From tab A"), default)).ShouldBeTrue();
        (await _repo.UpdateAsync(Changed(task, title: "From tab B"), default)).ShouldBeTrue();

        (await _repo.GetAsync(task.UserId, task.Id, default))!.Title.ShouldBe("From tab B");
    }
}
