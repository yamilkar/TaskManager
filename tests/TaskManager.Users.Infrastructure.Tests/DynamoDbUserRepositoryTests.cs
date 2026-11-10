using Amazon.DynamoDBv2;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Testing;
using TaskManager.Users.Domain;
using TaskManager.Users.Infrastructure.Persistence;

namespace TaskManager.Users.Infrastructure.Tests;

[CollectionDefinition("dynamodb")]
public sealed class DynamoDbCollection : ICollectionFixture<DynamoDbLocalFixture>;

[Collection("dynamodb")]
public sealed class DynamoDbUserRepositoryTests : IAsyncLifetime
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _table = $"Users-{Guid.NewGuid():N}";
    private readonly DynamoDbUserRepository _repo;

    public DynamoDbUserRepositoryTests(DynamoDbLocalFixture fixture)
    {
        _db = fixture.Client;
        _repo = new DynamoDbUserRepository(_db, Options.Create(new DynamoDbOptions { TableName = _table }));
    }

    public Task InitializeAsync() => TableBootstrapper.EnsureTableAsync(_db, UsersTableDefinition.Create(_table), CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    private static User NewUser(string email = "ana@x.com") =>
        User.Rehydrate(Guid.NewGuid(), "Ana", Email.Create(email).Value, "hash", DateTimeOffset.UtcNow);

    [Fact]
    public async Task TryAddAsync_then_GetByEmailAsync_returns_user()
    {
        var user = NewUser();

        (await _repo.TryAddAsync(user, default)).ShouldBeTrue();

        var found = await _repo.GetByEmailAsync(user.Email, default);
        found.ShouldNotBeNull();
        found.Id.ShouldBe(user.Id);
        found.FullName.ShouldBe("Ana");
        found.Email.Value.ShouldBe("ana@x.com");
    }

    [Fact]
    public async Task TryAddAsync_returns_false_for_duplicate_email_and_writes_no_second_user()
    {
        var first = NewUser();
        var second = NewUser();

        (await _repo.TryAddAsync(first, default)).ShouldBeTrue();
        (await _repo.TryAddAsync(second, default)).ShouldBeFalse();

        (await _repo.GetByIdAsync(second.Id, default)).ShouldBeNull();
    }

    [Fact]
    public async Task Concurrent_TryAddAsync_with_same_email_only_one_succeeds()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => _repo.TryAddAsync(NewUser(), default))));

        results.Count(r => r).ShouldBe(1);
        results.Count(r => !r).ShouldBe(4);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_unknown_id()
    {
        (await _repo.GetByIdAsync(Guid.NewGuid(), default)).ShouldBeNull();
    }

    [Fact]
    public async Task EnsureTableAsync_is_idempotent()
    {
        await TableBootstrapper.EnsureTableAsync(_db, UsersTableDefinition.Create(_table), default);
        await TableBootstrapper.EnsureTableAsync(_db, UsersTableDefinition.Create(_table), default);
    }
}
