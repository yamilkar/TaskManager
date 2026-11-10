using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application;

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal static class TaskMapping
{
    public static TaskResponse ToResponse(this TaskItem t) =>
        new(t.Id, t.Title, t.Description, t.Status.ToString(), t.DueDate, t.CreatedAt, t.UpdatedAt);
}
