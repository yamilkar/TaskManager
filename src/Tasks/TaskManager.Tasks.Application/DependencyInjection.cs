using Microsoft.Extensions.DependencyInjection;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.CreateTask;
using TaskManager.Tasks.Application.GetTask;
using TaskManager.Tasks.Application.ListTasks;

namespace TaskManager.Tasks.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTasksApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateTaskCommand, TaskResponse>, CreateTaskHandler>();
        services.AddScoped<IQueryHandler<ListTasksQuery, IReadOnlyList<TaskResponse>>, ListTasksHandler>();
        services.AddScoped<IQueryHandler<GetTaskQuery, TaskResponse>, GetTaskHandler>();
        return services;
    }
}
