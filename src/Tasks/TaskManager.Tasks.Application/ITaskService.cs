using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Contracts;

namespace TaskManager.Tasks.Application;

public interface ITaskService
{
    Task<Result<TaskResponse>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<TaskResponse>>> ListForUserAsync(Guid userId, CancellationToken ct);

    Task<Result<TaskResponse>> GetByIdAsync(Guid userId, Guid taskId, CancellationToken ct);
}
