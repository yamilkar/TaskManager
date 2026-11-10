using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.CreateTask;

/// <param name="UserId">The owner, taken from the JWT <c>sub</c> claim by the controller, never from the body.</param>
public sealed record CreateTaskCommand(Guid UserId, string? Title, string? Description, string? Status, DateOnly? DueDate);

public sealed class CreateTaskHandler(ITaskRepository tasks, IClock clock, IAppMetrics metrics)
    : ICommandHandler<CreateTaskCommand, TaskResponse>
{
    private const string StatusMessage = "Status must be one of: ToDo, InProgress, Done.";

    public async Task<Result<TaskResponse>> HandleAsync(CreateTaskCommand command, CancellationToken ct)
    {
        var status = ParseStatus(command.Status);

        var created = TaskItem.Create(
            Guid.NewGuid(), command.UserId, command.Title, command.Description,
            status ?? TaskItemStatus.ToDo, command.DueDate, clock.UtcNow);

        if (status is null)
        {
            // Report the status error together with any other field errors in one validation error.
            var errors = new Dictionary<string, string[]>(created.Error?.FieldErrors ?? new Dictionary<string, string[]>())
            {
                ["status"] = [StatusMessage],
            };
            return Error.Validation("validation_failed", "One or more validation errors occurred.", errors);
        }

        if (created.IsFailure)
        {
            return created.Error!;
        }

        await tasks.AddAsync(created.Value, ct);
        metrics.Increment(TasksMetrics.TaskCreated);
        return created.Value.ToResponse();
    }

    /// <summary>
    /// Returns ToDo when no status is given, the matching status for an exact (case-insensitive) name,
    /// and null for anything else, including numbers and comma lists.
    /// </summary>
    private static TaskItemStatus? ParseStatus(string? raw)
    {
        if (raw is null)
        {
            return TaskItemStatus.ToDo;
        }

        var name = Enum.GetNames<TaskItemStatus>()
            .FirstOrDefault(n => string.Equals(n, raw, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : Enum.Parse<TaskItemStatus>(name);
    }
}
