using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.Ports;

public interface ITaskRepository
{
    Task AddAsync(TaskItem task, CancellationToken ct);

    Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, CancellationToken ct);

    Task<TaskItem?> GetAsync(Guid userId, Guid taskId, CancellationToken ct);

    /// <summary>Replaces the stored task. Returns <c>false</c>, writing nothing, when it no longer exists.</summary>
    Task<bool> UpdateAsync(TaskItem task, CancellationToken ct);

    /// <summary>Removes the user's task. Returns <c>false</c>, deleting nothing, when no such task exists.</summary>
    Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken ct);
}
