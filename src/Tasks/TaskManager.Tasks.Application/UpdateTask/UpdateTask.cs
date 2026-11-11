using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;

namespace TaskManager.Tasks.Application.UpdateTask;

/// <param name="UserId">The owner, taken from the JWT <c>sub</c> claim by the controller, never from the body.</param>
/// <param name="TaskId">The task to replace, taken from the route.</param>
public sealed record UpdateTaskCommand(
    Guid UserId, Guid TaskId, string? Title, string? Description, string? Status, DateOnly? DueDate);

public sealed class UpdateTaskHandler(ITaskRepository tasks, IClock clock, IAppMetrics metrics)
    : ICommandHandler<UpdateTaskCommand, TaskResponse>
{
    public async Task<Result<TaskResponse>> HandleAsync(UpdateTaskCommand command, CancellationToken ct)
    {
        // The key is (userId, taskId), so another user's task is simply not found.
        var existing = await tasks.GetAsync(command.UserId, command.TaskId, ct);
        if (existing is null)
        {
            return TaskErrors.NotFound;
        }

        var updated = existing.Update(command.Title, command.Description, command.Status, command.DueDate, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        // False when the task was deleted after it was read; the conditional write created nothing.
        if (!await tasks.UpdateAsync(updated.Value, ct))
        {
            return TaskErrors.NotFound;
        }

        metrics.Increment(TasksMetrics.TaskUpdated);
        return updated.Value.ToResponse();
    }
}
