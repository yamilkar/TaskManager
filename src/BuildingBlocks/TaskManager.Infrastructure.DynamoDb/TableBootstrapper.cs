using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace TaskManager.Infrastructure.DynamoDb;

public static class TableBootstrapper
{
    public static async Task EnsureTableAsync(IAmazonDynamoDB db, CreateTableRequest request, CancellationToken ct)
    {
        try
        {
            await db.DescribeTableAsync(request.TableName, ct);
        }
        catch (ResourceNotFoundException)
        {
            try
            {
                await db.CreateTableAsync(request, ct);
            }
            catch (ResourceInUseException)
            {
                // Created concurrently by another caller; fall through to the wait.
            }
        }

        while (true)
        {
            var description = await db.DescribeTableAsync(request.TableName, ct);
            if (description.Table.TableStatus == TableStatus.ACTIVE)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }
    }
}
