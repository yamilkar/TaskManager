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
    private const int EmailGuardIndex = 1;
    private const string NotExists = "attribute_not_exists(pk)";

    private string Table => options.Value.TableName;

    public async Task<AddUserResult> TryAddAsync(User user, CancellationToken ct)
    {
        var request = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = Table, Item = UserItemMapper.ToUserItem(user), ConditionExpression = NotExists } },
                new TransactWriteItem { Put = new Put { TableName = Table, Item = UserItemMapper.ToEmailGuardItem(user), ConditionExpression = NotExists } },
            ],
        };

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

                // Reasons are positional. Only a failed email guard (index 1) means the email is taken;
                // a failed user item put (index 0) is unexpected and must surface.
                if (codes.Count > EmailGuardIndex && codes[EmailGuardIndex] == "ConditionalCheckFailed")
                {
                    return AddUserResult.EmailTaken;
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
