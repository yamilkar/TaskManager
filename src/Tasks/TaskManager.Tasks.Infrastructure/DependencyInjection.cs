using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Infrastructure.Seeding;

namespace TaskManager.Tasks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTasksInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDynamoDb(configuration);
        services.Configure<TaskSeedOptions>(configuration.GetSection(TaskSeedOptions.SectionName));

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITaskRepository, DynamoDbTaskRepository>();
        services.AddSingleton<TaskSeeder>();
        services.AddHostedService<TasksDatabaseBootstrapper>();

        return services;
    }
}
