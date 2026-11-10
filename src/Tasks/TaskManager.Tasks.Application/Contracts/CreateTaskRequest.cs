namespace TaskManager.Tasks.Application.Contracts;

public sealed record CreateTaskRequest(string? Title, string? Description, string? Status, DateOnly? DueDate);
