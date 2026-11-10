using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Infrastructure.Persistence;

public sealed class DynamoDbTaskRepository(IAmazonDynamoDB db, IOptions<DynamoDbOptions> options) : ITaskRepository
{
    private string Table => options.Value.TableName;

    public async Task AddAsync(TaskItem task, CancellationToken ct)
    {
        await db.PutItemAsync(
            new PutItemRequest
            {
                TableName = Table,
                Item = TaskItemMapper.ToItem(task),
                ConditionExpression = "attribute_not_exists(#id)",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#id"] = "_id" },
            },
            ct);
    }

    public async Task<TaskPageResult> ListByUserAsync(Guid userId, int limit, string? cursor, CancellationToken ct)
    {
        var response = await db.QueryAsync(
            new QueryRequest
            {
                TableName = Table,
                IndexName = TasksTableDefinition.ByCreatedAtIndex,
                ConsistentRead = true,
                ScanIndexForward = false,
                Limit = limit,
                KeyConditionExpression = "userId = :u",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":u"] = userId.ToAttr() },
                ExclusiveStartKey = cursor is null ? null : TaskCursor.Decode(cursor, userId),
            },
            ct);

        var nextCursor = response.LastEvaluatedKey is { Count: > 0 } ? TaskCursor.Encode(response.LastEvaluatedKey) : null;
        return new TaskPageResult(response.Items.Select(TaskItemMapper.FromItem).ToList(), nextCursor);
    }

    public async Task<TaskItem?> GetAsync(Guid userId, Guid taskId, CancellationToken ct)
    {
        var response = await db.GetItemAsync(
            new GetItemRequest
            {
                TableName = Table,
                ConsistentRead = true,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["userId"] = userId.ToAttr(),
                    ["_id"] = taskId.ToAttr(),
                },
            },
            ct);

        return response.IsItemSet ? TaskItemMapper.FromItem(response.Item) : null;
    }
}
