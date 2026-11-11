using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.Ports;

public interface ITaskRepository
{
    Task AddAsync(TaskItem task, CancellationToken ct);

    Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, CancellationToken ct);

    Task<TaskItem?> GetAsync(Guid userId, Guid taskId, CancellationToken ct);

    /// <summary>Replaces the stored task. Returns <c>false</c>, writing nothing, when it no longer exists.</summary>
    Task<bool> UpdateAsync(TaskItem task, CancellationToken ct);
}
