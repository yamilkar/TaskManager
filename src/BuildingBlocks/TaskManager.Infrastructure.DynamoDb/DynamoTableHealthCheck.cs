using Amazon.DynamoDBv2;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace TaskManager.Infrastructure.DynamoDb;

public sealed class DynamoTableHealthCheck(IAmazonDynamoDB db, IOptions<DynamoDbOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.DescribeTableAsync(options.Value.TableName, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("DynamoDB table is not reachable.", ex);
        }
    }
}
