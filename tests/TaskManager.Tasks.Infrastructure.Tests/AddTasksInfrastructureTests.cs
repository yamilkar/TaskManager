using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TaskManager.SharedKernel.Security;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Infrastructure.Seeding;

namespace TaskManager.Tasks.Infrastructure.Tests;

public sealed class AddTasksInfrastructureTests
{
    [Fact]
    public void Services_resolve_with_in_memory_configuration_and_do_not_register_jwt()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dynamo:ServiceUrl"] = "http://localhost:8000",
            ["Dynamo:TableName"] = "TaskManager-Tasks",
        }).Build();

        var services = new ServiceCollection().AddTasksInfrastructure(config);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITaskRepository>().ShouldNotBeNull();
        provider.GetRequiredService<TaskSeeder>().ShouldNotBeNull();
        services.Any(d => d.ServiceType == typeof(IConfigureOptions<JwtOptions>)).ShouldBeFalse();
    }
}
