using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;

namespace TaskManager.Tasks.Application.DeleteTask;

/// <param name="UserId">The owner, taken from the JWT <c>sub</c> claim by the controller, never from the request.</param>
/// <param name="TaskId">The task to remove, taken from the route.</param>
public sealed record DeleteTaskCommand(Guid UserId, Guid TaskId);

public sealed class DeleteTaskHandler(ITaskRepository tasks, IAppMetrics metrics)
    : ICommandHandler<DeleteTaskCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteTaskCommand command, CancellationToken ct)
    {
        // The key is (userId, taskId), so another user's task is simply not found.
        if (!await tasks.DeleteAsync(command.UserId, command.TaskId, ct))
        {
            return TaskErrors.NotFound;
        }

        metrics.Increment(TasksMetrics.TaskDeleted);
        return default(Unit);
    }
}
