using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TaskManager.Infrastructure.DynamoDb;

public static class DynamoDbServiceCollectionExtensions
{
    public static IServiceCollection AddDynamoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DynamoDbOptions.SectionName).Get<DynamoDbOptions>() ?? new DynamoDbOptions();

        services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));

        services.AddSingleton<IAmazonDynamoDB>(_ =>
        {
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                return new AmazonDynamoDBClient(
                    new BasicAWSCredentials("local", "local"),
                    new AmazonDynamoDBConfig
                    {
                        ServiceURL = options.ServiceUrl,
                        AuthenticationRegion = options.Region,
                    });
            }

            return new AmazonDynamoDBClient(new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region) });
        });

        return services;
    }
}
