using Amazon.DynamoDBv2;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Testing;
using TaskManager.Users.Domain;
using TaskManager.Users.Infrastructure.Persistence;
using TaskManager.Users.Infrastructure.Security;
using TaskManager.Users.Infrastructure.Seeding;

namespace TaskManager.Users.Infrastructure.Tests;

[Collection("dynamodb")]
public sealed class UserSeederTests : IAsyncLifetime
{
    private static readonly Guid DemoId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IAmazonDynamoDB _db;
    private readonly string _table = $"Users-{Guid.NewGuid():N}";
    private readonly DynamoDbUserRepository _repo;
    private readonly AspNetPasswordHasher _hasher = new();
    private readonly UserSeeder _seeder;

    public UserSeederTests(DynamoDbLocalFixture fixture)
    {
        _db = fixture.Client;
        _repo = new DynamoDbUserRepository(_db, Options.Create(new DynamoDbOptions { TableName = _table }));
        _seeder = new UserSeeder(_repo, _hasher, new FixedClock(DateTimeOffset.UtcNow), Options.Create(new UserSeedOptions()));
    }

    public Task InitializeAsync() => TableBootstrapper.EnsureTableAsync(_db, UsersTableDefinition.Create(_table), CancellationToken.None);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedAsync_creates_demo_user_with_fixed_id_and_verifiable_password()
    {
        await _seeder.SeedAsync(default);

        var user = await _repo.GetByEmailAsync(Email.Create("demo@taskmanager.dev").Value, default);
        user.ShouldNotBeNull();
        user.Id.ShouldBe(DemoId);
        user.FullName.ShouldBe("Demo User");
        _hasher.Verify(user.PasswordHash, "Demo123!").ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_twice_is_idempotent()
    {
        await _seeder.SeedAsync(default);
        await Should.NotThrowAsync(() => _seeder.SeedAsync(default));

        (await _repo.GetByIdAsync(DemoId, default)).ShouldNotBeNull();
    }
}
