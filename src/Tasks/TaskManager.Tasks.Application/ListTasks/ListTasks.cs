using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;

namespace TaskManager.Tasks.Application.ListTasks;

public sealed record ListTasksQuery(Guid UserId);

public sealed class ListTasksHandler(ITaskRepository tasks) : IQueryHandler<ListTasksQuery, IReadOnlyList<TaskResponse>>
{
    public async Task<Result<IReadOnlyList<TaskResponse>>> HandleAsync(ListTasksQuery query, CancellationToken ct)
    {
        var items = await tasks.ListByUserAsync(query.UserId, ct);
        IReadOnlyList<TaskResponse> responses = items
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Select(t => t.ToResponse())
            .ToList();
        return Result<IReadOnlyList<TaskResponse>>.Success(responses);
    }
}
