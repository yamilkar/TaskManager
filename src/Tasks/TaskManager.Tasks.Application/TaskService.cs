using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Contracts;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application;

public sealed class TaskService(ITaskRepository tasks, IClock clock, IAppMetrics metrics) : ITaskService
{
    private const string StatusMessage = "Status must be one of: ToDo, InProgress, Done.";

    public async Task<Result<TaskResponse>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct)
    {
        var statusValid = TryParseStatus(request.Status, out var status);

        var created = TaskItem.Create(
            Guid.NewGuid(), userId, request.Title, request.Description, status, request.DueDate, clock.UtcNow);

        if (!statusValid)
        {
            // Report the status error together with any other field errors in one validation error.
            var errors = new Dictionary<string, string[]>();
            if (created.Error?.FieldErrors is { } other)
            {
                foreach (var (key, value) in other)
                {
                    errors[key] = value;
                }
            }

            errors["status"] = [StatusMessage];
            return Error.Validation("validation_failed", "One or more validation errors occurred.", errors);
        }

        if (created.IsFailure)
        {
            return created.Error!;
        }

        await tasks.AddAsync(created.Value, ct);
        metrics.Increment(TasksMetrics.TaskCreated);
        return ToResponse(created.Value);
    }

    public async Task<Result<IReadOnlyList<TaskResponse>>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var items = await tasks.ListByUserAsync(userId, ct);
        IReadOnlyList<TaskResponse> responses = items
            .OrderByDescending(t => t.CreatedAt)
            .Select(ToResponse)
            .ToList();
        return Result<IReadOnlyList<TaskResponse>>.Success(responses);
    }

    public async Task<Result<TaskResponse>> GetByIdAsync(Guid userId, Guid taskId, CancellationToken ct)
    {
        var task = await tasks.GetAsync(userId, taskId, ct);
        return task is null ? TaskErrors.NotFound : ToResponse(task);
    }

    private static bool TryParseStatus(string? raw, out TaskItemStatus status)
    {
        status = TaskItemStatus.ToDo;
        if (raw is null)
        {
            return true;
        }

        // Only names are accepted: numeric strings (even in-range ones like "1") are rejected.
        return !raw.Any(char.IsDigit) && Enum.TryParse(raw, ignoreCase: true, out status) && Enum.IsDefined(status);
    }

    private static TaskResponse ToResponse(TaskItem t) =>
        new(t.Id, t.Title, t.Description, t.Status.ToString(), t.DueDate, t.CreatedAt, t.UpdatedAt);
}
