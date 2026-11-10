namespace TaskManager.Tasks.Application.Contracts;

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
