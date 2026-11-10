using Amazon.DynamoDBv2;
using TaskManager.Tasks.Infrastructure.Persistence;

namespace TaskManager.Tasks.Infrastructure.Tests;

public sealed class TasksTableDefinitionTests
{
    [Fact]
    public void Create_anyName_declaresByCreatedAtLsiOnUserIdAndCreatedAtId()
    {
        var lsi = TasksTableDefinition.Create("T").LocalSecondaryIndexes.ShouldHaveSingleItem();
        lsi.IndexName.ShouldBe("ByCreatedAt");
        lsi.KeySchema.Select(k => (k.AttributeName, k.KeyType.Value)).ShouldBe([("userId", "HASH"), ("createdAtId", "RANGE")]);
        lsi.Projection.ProjectionType.ShouldBe(ProjectionType.ALL);
    }
}
