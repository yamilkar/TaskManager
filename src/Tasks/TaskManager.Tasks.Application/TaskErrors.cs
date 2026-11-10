using TaskManager.SharedKernel;

namespace TaskManager.Tasks.Application;

public static class TaskErrors
{
    public static readonly Error NotFound = Error.NotFound("tasks.not_found", "Task not found.");
}
