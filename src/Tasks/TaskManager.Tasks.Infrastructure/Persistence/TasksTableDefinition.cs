using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace TaskManager.Tasks.Infrastructure.Persistence;

public static class TasksTableDefinition
{
    public static CreateTableRequest Create(string tableName) => new()
    {
        TableName = tableName,
        BillingMode = BillingMode.PAY_PER_REQUEST,
        AttributeDefinitions =
        [
            new AttributeDefinition("userId", ScalarAttributeType.S),
            new AttributeDefinition("_id", ScalarAttributeType.S),
        ],
        KeySchema =
        [
            new KeySchemaElement("userId", KeyType.HASH),
            new KeySchemaElement("_id", KeyType.RANGE),
        ],
    };
}
