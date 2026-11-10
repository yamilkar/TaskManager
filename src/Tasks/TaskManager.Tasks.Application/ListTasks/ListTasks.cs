using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;

namespace TaskManager.Tasks.Application.ListTasks;

public sealed record ListTasksQuery(Guid UserId, int? Limit, string? Cursor);

public sealed record TaskPage(IReadOnlyList<TaskResponse> Items, string? NextCursor);

public sealed class ListTasksHandler(ITaskRepository tasks) : IQueryHandler<ListTasksQuery, TaskPage>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public async Task<Result<TaskPage>> HandleAsync(ListTasksQuery query, CancellationToken ct)
    {
        var limit = query.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            return Result<TaskPage>.Failure(FieldError("limit", $"Limit must be between 1 and {MaxLimit}."));
        }

        try
        {
            var page = await tasks.ListByUserAsync(query.UserId, limit, query.Cursor, ct);
            return Result<TaskPage>.Success(new TaskPage(page.Items.Select(t => t.ToResponse()).ToList(), page.NextCursor));
        }
        catch (InvalidCursorException)
        {
            return Result<TaskPage>.Failure(FieldError("cursor", "Cursor is invalid."));
        }
    }

    private static Error FieldError(string field, string message) =>
        Error.Validation(
            "validation_failed",
            "One or more validation errors occurred.",
            new Dictionary<string, string[]> { [field] = [message] });
}
