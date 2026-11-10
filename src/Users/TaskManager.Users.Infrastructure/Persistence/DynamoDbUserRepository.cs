using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Infrastructure.Persistence;

public sealed class DynamoDbUserRepository(IAmazonDynamoDB db, IOptions<DynamoDbOptions> options) : IUserRepository
{
    private const int MaxAttempts = 3;

    // Positions of the puts in TransactItems. DynamoDB reports CancellationReasons in the same order,
    // so this order is load-bearing: the request and the reason lookup both use these constants.
    private const int UserItemIndex = 0;
    private const int EmailGuardIndex = 1;
    private const string ConditionalCheckFailed = "ConditionalCheckFailed";
    private const string NotExists = "attribute_not_exists(pk)";

    private string Table => options.Value.TableName;

    public async Task<AddUserResult> TryAddAsync(User user, CancellationToken ct)
    {
        var items = new TransactWriteItem[2];
        items[UserItemIndex] = new TransactWriteItem { Put = new Put { TableName = Table, Item = UserItemMapper.ToUserItem(user), ConditionExpression = NotExists } };
        items[EmailGuardIndex] = new TransactWriteItem { Put = new Put { TableName = Table, Item = UserItemMapper.ToEmailGuardItem(user), ConditionExpression = NotExists } };
        var request = new TransactWriteItemsRequest { TransactItems = [.. items] };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.TransactWriteItemsAsync(request, ct);
                return AddUserResult.Added;
            }
            catch (TransactionCanceledException ex)
            {
                var codes = ex.CancellationReasons?.Select(r => r.Code).ToList() ?? [];

                // The email is taken only when the guard failed and the user item did not. A failed user
                // item put (a duplicate user id) is unexpected and must surface, even if the guard failed too.
                var guardFailed = codes.Count > EmailGuardIndex && codes[EmailGuardIndex] == ConditionalCheckFailed;
                var userItemFailed = codes.Count > UserItemIndex && codes[UserItemIndex] == ConditionalCheckFailed;
                if (guardFailed && !userItemFailed)
                {
                    return AddUserResult.EmailTaken;
                }

                if (userItemFailed)
                {
                    throw;
                }

                if (codes.Contains("TransactionConflict"))
                {
                    if (attempt >= MaxAttempts)
                    {
                        return AddUserResult.Busy;
                    }

                    await Task.Delay(Random.Shared.Next(10, 60) * attempt, ct);
                    continue;
                }

                throw;
            }
        }
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct)
    {
        var guard = await GetAsync(UserItemMapper.EmailKey(email), ct);
        if (guard is null || !guard.TryGetValue("userId", out var userId) || !Guid.TryParse(userId.S, out var id))
        {
            return null;
        }

        return await GetByIdAsync(id, ct);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await GetAsync(UserItemMapper.UserKey(id), ct);
        return item is null ? null : UserItemMapper.FromUserItem(item);
    }

    private async Task<Dictionary<string, AttributeValue>?> GetAsync(string pk, CancellationToken ct)
    {
        var response = await db.GetItemAsync(
            new GetItemRequest
            {
                TableName = Table,
                ConsistentRead = true,
                Key = new Dictionary<string, AttributeValue> { ["pk"] = pk.ToAttr() },
            },
            ct);

        return response.IsItemSet ? response.Item : null;
    }
}
