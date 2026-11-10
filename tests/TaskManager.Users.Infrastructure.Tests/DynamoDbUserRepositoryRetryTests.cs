using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;
using TaskManager.Users.Infrastructure.Persistence;

namespace TaskManager.Users.Infrastructure.Tests;

/// <summary>Retry behavior with a substituted client: DynamoDB Local cannot be made to conflict on demand.</summary>
public sealed class DynamoDbUserRepositoryRetryTests
{
    private readonly IAmazonDynamoDB _db = Substitute.For<IAmazonDynamoDB>();

    private DynamoDbUserRepository Sut => new(_db, Options.Create(new DynamoDbOptions { TableName = "Users" }));

    private static User NewUser() =>
        User.Create(Guid.NewGuid(), "Ana", Email.Create("ana@x.com").Value, "HASH", DateTimeOffset.UtcNow).Value;

    private static TransactionCanceledException Canceled(string code) =>
        new("canceled") { CancellationReasons = [new CancellationReason { Code = code }] };

    [Fact]
    public async Task TryAddAsync_transactionConflictOnEveryAttempt_returnsBusyAfterThreeAttempts()
    {
        _db.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Throws(Canceled("TransactionConflict"));

        var result = await Sut.TryAddAsync(NewUser(), default);

        result.ShouldBe(AddUserResult.Busy);
        await _db.Received(3).TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAddAsync_conflictThenSuccess_returnsAdded()
    {
        _db.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw Canceled("TransactionConflict"),
                _ => Task.FromResult(new TransactWriteItemsResponse()));

        var result = await Sut.TryAddAsync(NewUser(), default);

        result.ShouldBe(AddUserResult.Added);
    }

    [Fact]
    public async Task TryAddAsync_otherCancellationReason_rethrows()
    {
        _db.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Throws(Canceled("ValidationError"));

        await Should.ThrowAsync<TransactionCanceledException>(() => Sut.TryAddAsync(NewUser(), default));
    }
}
