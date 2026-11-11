using TaskManager.SharedKernel;

namespace TaskManager.Tasks.Domain;

public sealed class TaskItem
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    /// <summary>Must stay identical to the web client's <c>DUE_DATE_IN_PAST_MESSAGE</c> (spec D18); both sides pin it in a test.</summary>
    public const string DueDateInPastMessage = "Due date cannot be in the past. Choose today or a later date.";

    private TaskItem(
        Guid id,
        Guid userId,
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Description = description;
        Status = status;
        DueDate = dueDate;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public Guid UserId { get; }
    public string Title { get; }
    public string? Description { get; }
    public TaskItemStatus Status { get; }
    public DateOnly? DueDate { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static Result<TaskItem> Create(
        Guid id,
        Guid userId,
        string? title,
        string? description,
        string? status,
        DateOnly? dueDate,
        DateTimeOffset now)
    {
        // Ids come from code (Guid.NewGuid, the token's sub), never from user input, so an empty one is a bug.
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Task id must not be empty.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Task owner must not be empty.", nameof(userId));
        }

        var trimmedTitle = title?.Trim();
        var trimmedDescription = Normalize(description);
        var errors = Validate(trimmedTitle, trimmedDescription, status, statusRequired: false, dueDate, unchangedDueDate: null, now, out var parsedStatus);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        return new TaskItem(id, userId, trimmedTitle!, trimmedDescription, parsedStatus, dueDate, now, now);
    }

    /// <summary>
    /// Replaces the editable fields with the same rules as <see cref="Create"/>, except that status is required
    /// and an unchanged due date is accepted even if it is now in the past (spec D23, D24).
    /// Returns a new item; id, owner and creation time are kept.
    /// </summary>
    public Result<TaskItem> Update(string? title, string? description, string? status, DateOnly? dueDate, DateTimeOffset now)
    {
        var trimmedTitle = title?.Trim();
        var trimmedDescription = Normalize(description);
        var errors = Validate(trimmedTitle, trimmedDescription, status, statusRequired: true, dueDate, unchangedDueDate: DueDate, now, out var parsedStatus);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        return new TaskItem(Id, UserId, trimmedTitle!, trimmedDescription, parsedStatus, dueDate, CreatedAt, now);
    }

    private static string? Normalize(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static Error ValidationFailed(Dictionary<string, string[]> errors) =>
        Error.Validation("validation_failed", "One or more validation errors occurred.", errors);

    private static Dictionary<string, string[]> Validate(
        string? trimmedTitle,
        string? trimmedDescription,
        string? status,
        bool statusRequired,
        DateOnly? dueDate,
        DateOnly? unchangedDueDate,
        DateTimeOffset now,
        out TaskItemStatus parsedStatus)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrEmpty(trimmedTitle))
        {
            errors["title"] = ["Title is required."];
        }
        else if (trimmedTitle.Length > TitleMaxLength)
        {
            errors["title"] = ["Title must be at most 200 characters."];
        }

        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            errors["description"] = ["Description must be at most 2000 characters."];
        }

        if ((statusRequired && status is null) || !TaskItemStatuses.TryParse(status, out parsedStatus))
        {
            parsedStatus = TaskItemStatus.ToDo;
            errors["status"] = [TaskItemStatuses.InvalidMessage];
        }

        // Spec D9: valid if not before today in the user's time zone; UTC today minus one day covers every UTC offset.
        // Spec D24: an unchanged due date is kept as is, so an overdue task can still be edited or moved.
        if (dueDate is { } due && dueDate != unchangedDueDate && due < DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1))
        {
            errors["dueDate"] = [DueDateInPastMessage];
        }

        return errors;
    }

    public static TaskItem Rehydrate(
        Guid id,
        Guid userId,
        string title,
        string? description,
        TaskItemStatus status,
        DateOnly? dueDate,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        => new(id, userId, title, description, status, dueDate, createdAt, updatedAt);
}
