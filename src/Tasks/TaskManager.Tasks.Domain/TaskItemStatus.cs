namespace TaskManager.Tasks.Domain;

public enum TaskItemStatus
{
    ToDo,
    InProgress,
    Done,
}

public static class TaskItemStatuses
{
    public const string InvalidMessage = "Status must be one of: ToDo, InProgress, Done.";

    /// <summary>
    /// No status means <see cref="TaskItemStatus.ToDo"/>. Otherwise only an exact status name is accepted,
    /// ignoring case; numbers, comma lists and surrounding whitespace are rejected.
    /// </summary>
    public static bool TryParse(string? raw, out TaskItemStatus status)
    {
        status = TaskItemStatus.ToDo;
        if (raw is null)
        {
            return true;
        }

        var name = Enum.GetNames<TaskItemStatus>()
            .FirstOrDefault(n => string.Equals(n, raw, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return false;
        }

        status = Enum.Parse<TaskItemStatus>(name);
        return true;
    }
}
