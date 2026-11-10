using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.Ports;

public interface ITaskRepository
{
    Task AddAsync(TaskItem task, CancellationToken ct);

    /// <summary>Returns one page of the user's tasks, newest first. Throws <see cref="InvalidCursorException"/> for a bad cursor.</summary>
    Task<TaskPageResult> ListByUserAsync(Guid userId, int limit, string? cursor, CancellationToken ct);

    Task<TaskItem?> GetAsync(Guid userId, Guid taskId, CancellationToken ct);
}
