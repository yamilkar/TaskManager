using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.Ports;

public interface ITaskRepository
{
    Task AddAsync(TaskItem task, CancellationToken ct);

    Task<IReadOnlyList<TaskItem>> ListByUserAsync(Guid userId, CancellationToken ct);

    Task<TaskItem?> GetAsync(Guid userId, Guid taskId, CancellationToken ct);
}
