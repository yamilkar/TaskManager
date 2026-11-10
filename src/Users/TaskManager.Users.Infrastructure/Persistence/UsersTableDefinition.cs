using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace TaskManager.Users.Infrastructure.Persistence;

public static class UsersTableDefinition
{
    public static CreateTableRequest Create(string tableName) => new()
    {
        TableName = tableName,
        BillingMode = BillingMode.PAY_PER_REQUEST,
        AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
        KeySchema = [new KeySchemaElement("pk", KeyType.HASH)],
    };
}
