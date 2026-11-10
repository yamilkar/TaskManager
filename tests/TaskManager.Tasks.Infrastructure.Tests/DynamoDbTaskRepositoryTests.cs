using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Tasks.Application.Ports;
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
    public async Task GetAsync_with_other_users_id_returns_null()
    {
        var task = NewTask(Guid.NewGuid());
        await _repo.AddAsync(task, default);

        (await _repo.GetAsync(Guid.NewGuid(), task.Id, default)).ShouldBeNull();
    }

    private static TaskItem NewTaskAt(Guid userId, DateTimeOffset createdAt, Guid? id = null) =>
        TaskItem.Rehydrate(id ?? Guid.NewGuid(), userId, "Task", null, TaskItemStatus.ToDo, null, createdAt, createdAt);

    [Fact]
    public async Task ListByUserAsync_25Tasks_returnsNewestFirstPagesOf20Then5()
    {
        var user = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-11-10T08:00:00Z");
        for (var i = 0; i < 25; i++)
        {
            await _repo.AddAsync(NewTaskAt(user, start.AddSeconds(i)), default);
        }

        var page1 = await _repo.ListByUserAsync(user, 20, null, default);
        page1.Items.Count.ShouldBe(20);
        page1.Items.Select(t => t.CreatedAt).ToList().ShouldBe(page1.Items.Select(t => t.CreatedAt).OrderByDescending(d => d).ToList());
        page1.Items[0].CreatedAt.ShouldBe(start.AddSeconds(24));
        page1.NextCursor.ShouldNotBeNull();

        var page2 = await _repo.ListByUserAsync(user, 20, page1.NextCursor, default);
        page2.Items.Count.ShouldBe(5);
        page2.Items[^1].CreatedAt.ShouldBe(start);
        if (page2.NextCursor is not null)
        {
            var page3 = await _repo.ListByUserAsync(user, 20, page2.NextCursor, default);
            page3.Items.Count.ShouldBe(0);
            page3.NextCursor.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ListByUserAsync_sameCreatedAt_ordersByIdDescending()
    {
        var user = Guid.NewGuid();
        var at = DateTimeOffset.Parse("2026-11-10T08:00:00Z");
        var low = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await _repo.AddAsync(NewTaskAt(user, at, low), default);
        await _repo.AddAsync(NewTaskAt(user, at, high), default);

        var page = await _repo.ListByUserAsync(user, 20, null, default);

        page.Items.Select(t => t.Id).ToList().ShouldBe([high, low]);
    }

    [Fact]
    public async Task ListByUserAsync_otherUsersTasks_neverAppearOnAnyPage()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var at = DateTimeOffset.Parse("2026-11-10T08:00:00Z");
        for (var i = 0; i < 3; i++)
        {
            await _repo.AddAsync(NewTaskAt(a, at.AddSeconds(i)), default);
            await _repo.AddAsync(NewTaskAt(b, at.AddSeconds(i)), default);
        }

        var seen = new List<TaskItem>();
        string? cursor = null;
        do
        {
            var page = await _repo.ListByUserAsync(a, 2, cursor, default);
            seen.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        seen.Count.ShouldBe(3);
        seen.ShouldAllBe(t => t.UserId == a);
    }

    [Fact]
    public async Task ListByUserAsync_cursorFromAnotherUser_throwsInvalidCursor()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var at = DateTimeOffset.Parse("2026-11-10T08:00:00Z");
        for (var i = 0; i < 3; i++)
        {
            await _repo.AddAsync(NewTaskAt(a, at.AddSeconds(i)), default);
        }

        var page = await _repo.ListByUserAsync(a, 1, null, default);

        await Should.ThrowAsync<InvalidCursorException>(() => _repo.ListByUserAsync(b, 1, page.NextCursor, default));
    }

    [Fact]
    public async Task AddAsync_rejects_duplicate_id()
    {
        var task = NewTask(Guid.NewGuid());
        await _repo.AddAsync(task, default);

        await Should.ThrowAsync<ConditionalCheckFailedException>(() => _repo.AddAsync(task, default));
    }
}
