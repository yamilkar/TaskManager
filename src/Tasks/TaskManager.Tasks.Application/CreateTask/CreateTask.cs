using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.CreateTask;

/// <param name="UserId">The owner, taken from the JWT <c>sub</c> claim by the controller, never from the body.</param>
public sealed record CreateTaskCommand(Guid UserId, string? Title, string? Description, string? Status, DateOnly? DueDate);

public sealed class CreateTaskHandler(ITaskRepository tasks, IClock clock, IAppMetrics metrics)
    : ICommandHandler<CreateTaskCommand, TaskResponse>
{
    public async Task<Result<TaskResponse>> HandleAsync(CreateTaskCommand command, CancellationToken ct)
    {
        // All field rules (title, description, status, due date) live in the domain and come back as one error.
        var created = TaskItem.Create(
            Guid.NewGuid(), command.UserId, command.Title, command.Description, command.Status, command.DueDate, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        await tasks.AddAsync(created.Value, ct);
        metrics.Increment(TasksMetrics.TaskCreated);
        return created.Value.ToResponse();
    }
}
