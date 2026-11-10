using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;

namespace TaskManager.Tasks.Application.GetTask;

public sealed record GetTaskQuery(Guid UserId, Guid TaskId);

public sealed class GetTaskHandler(ITaskRepository tasks) : IQueryHandler<GetTaskQuery, TaskResponse>
{
    public async Task<Result<TaskResponse>> HandleAsync(GetTaskQuery query, CancellationToken ct)
    {
        // The key is (userId, taskId), so another user's task is simply not found.
        var task = await tasks.GetAsync(query.UserId, query.TaskId, ct);
        return task is null ? TaskErrors.NotFound : task.ToResponse();
    }
}
