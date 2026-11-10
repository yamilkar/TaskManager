using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Testcontainers.DynamoDb;
using Xunit;

namespace TaskManager.Testing;

/// <summary>
/// Provides a DynamoDB Local client. Starts a Testcontainers container by default; when the
/// DYNAMODB_TEST_ENDPOINT env var is set, connects to that endpoint instead (e.g. docker compose).
/// Share via an xUnit collection fixture.
/// </summary>
public sealed class DynamoDbLocalFixture : IAsyncLifetime
{
    public const string EndpointVariable = "DYNAMODB_TEST_ENDPOINT";

    private DynamoDbContainer? _container;

    public IAmazonDynamoDB Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable);

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _container = new DynamoDbBuilder("amazon/dynamodb-local:2.5.3").Build();
            await _container.StartAsync();
            Client = CreateClient(_container.GetConnectionString(), TimeSpan.FromSeconds(30));
            return;
        }

        Client = CreateClient(endpoint, TimeSpan.FromSeconds(5));
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Client.ListTablesAsync(cts.Token);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DynamoDB Local is not reachable at {endpoint}. Run `docker compose up -d` first.", ex);
        }
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static AmazonDynamoDBClient CreateClient(string endpoint, TimeSpan timeout) =>
        new(
            new BasicAWSCredentials("local", "local"),
            new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1", Timeout = timeout });
}
