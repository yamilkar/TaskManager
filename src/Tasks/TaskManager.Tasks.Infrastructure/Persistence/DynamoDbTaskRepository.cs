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

    public async Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, CancellationToken ct)
    {
        var tasks = new List<TaskItem>();
        Dictionary<string, AttributeValue>? startKey = null;

        do
        {
            var response = await db.QueryAsync(
                new QueryRequest
                {
                    TableName = Table,
                    ConsistentRead = true,
                    KeyConditionExpression = "userId = :u",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":u"] = userId.ToAttr() },
                    ExclusiveStartKey = startKey,
                },
                ct);

            tasks.AddRange(response.Items.Select(TaskItemMapper.FromItem));
            startKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (startKey is not null);

        return tasks;
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
