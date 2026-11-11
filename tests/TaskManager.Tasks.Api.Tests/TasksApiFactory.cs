using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application;
using TaskManager.Tasks.Application.CreateTask;
using TaskManager.Tasks.Application.GetTask;
using TaskManager.Tasks.Application.ListTasks;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Application.UpdateTask;
using TaskManager.Tasks.Infrastructure;

namespace TaskManager.Tasks.Api.Tests;

/// <summary>
/// Hosts the Tasks API in the Testing environment. By default every handler is a substitute;
/// with <c>useRealHandlers</c> the real handlers run on top of a substituted <see cref="ITaskRepository"/>.
/// </summary>
public sealed class TasksApiFactory(string signingKey = TasksApiFactory.DefaultKey, bool useRealHandlers = false)
    : WebApplicationFactory<Program>
{
    public const string DefaultKey = "test-signing-key-0123456789-abcdefghijklmnop";

    public string SigningKey { get; } = signingKey;

    public ICommandHandler<CreateTaskCommand, TaskResponse> Create { get; } =
        Substitute.For<ICommandHandler<CreateTaskCommand, TaskResponse>>();

    public IQueryHandler<ListTasksQuery, IReadOnlyList<TaskResponse>> List { get; } =
        Substitute.For<IQueryHandler<ListTasksQuery, IReadOnlyList<TaskResponse>>>();

    public IQueryHandler<GetTaskQuery, TaskResponse> Get { get; } =
        Substitute.For<IQueryHandler<GetTaskQuery, TaskResponse>>();

    public ICommandHandler<UpdateTaskCommand, TaskResponse> Update { get; } =
        Substitute.For<ICommandHandler<UpdateTaskCommand, TaskResponse>>();

    public ITaskRepository Repository { get; } = Substitute.For<ITaskRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = SigningKey }));

        builder.ConfigureServices(services =>
        {
            if (!useRealHandlers)
            {
                Replace(services, Create);
                Replace(services, List);
                Replace(services, Get);
                Replace(services, Update);
            }

            Replace(services, Repository);
            Replace(services, Substitute.For<IAmazonDynamoDB>());

            var bootstrappers = services.Where(d =>
                d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TasksDatabaseBootstrapper)).ToList();
            foreach (var descriptor in bootstrappers)
            {
                services.Remove(descriptor);
            }
        });
    }

    private static void Replace<T>(IServiceCollection services, T instance) where T : class
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(instance);
    }
}
