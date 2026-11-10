using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.Ports;

public sealed record TaskPageResult(IReadOnlyList<TaskItem> Items, string? NextCursor);
