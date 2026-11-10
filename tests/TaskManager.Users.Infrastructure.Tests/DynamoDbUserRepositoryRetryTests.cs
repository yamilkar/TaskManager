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

    // Reasons are positional: index 0 is the user item put, index 1 is the email guard put.
    private static TransactionCanceledException Canceled(params string[] codes) =>
        new("canceled") { CancellationReasons = codes.Select(c => new CancellationReason { Code = c }).ToList() };

    [Fact]
    public async Task TryAddAsync_conditionalCheckFailedOnEmailGuard_returnsEmailTaken()
    {
        _db.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Throws(Canceled("None", "ConditionalCheckFailed"));

        var result = await Sut.TryAddAsync(NewUser(), default);

        result.ShouldBe(AddUserResult.EmailTaken);
    }

    [Fact]
    public async Task TryAddAsync_conditionalCheckFailedOnUserItemOnly_rethrows()
    {
        _db.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Throws(Canceled("ConditionalCheckFailed", "None"));

        await Should.ThrowAsync<TransactionCanceledException>(() => Sut.TryAddAsync(NewUser(), default));
    }

    [Fact]
    public async Task TryAddAsync_cancelledDuringRetryDelay_throwsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        _db.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<TransactWriteItemsResponse>>(_ =>
            {
                cts.Cancel();
                throw Canceled("TransactionConflict");
            });

        var thrown = await Record.ExceptionAsync(() => Sut.TryAddAsync(NewUser(), cts.Token));

        thrown.ShouldBeAssignableTo<OperationCanceledException>();
        await _db.Received(1).TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>());
    }

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
