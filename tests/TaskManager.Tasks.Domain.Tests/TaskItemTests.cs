namespace TaskManager.Tasks.Domain.Tests;

public class TaskItemTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_title_sets_ToDo_and_equal_timestamps()
    {
        var task = TaskItem.Create(Id, UserId, "  Write tests  ", "  desc  ", TaskItemStatus.ToDo, null, Now).Value;

        task.Id.ShouldBe(Id);
        task.UserId.ShouldBe(UserId);
        task.Title.ShouldBe("Write tests");
        task.Description.ShouldBe("desc");
        task.Status.ShouldBe(TaskItemStatus.ToDo);
        task.DueDate.ShouldBeNull();
        task.CreatedAt.ShouldBe(Now);
        task.UpdatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_title(string? title)
    {
        var result = TaskItem.Create(Id, UserId, title, null, TaskItemStatus.ToDo, null, Now);

        result.IsFailure.ShouldBeTrue();
        result.Error!.FieldErrors!["title"].ShouldContain("Title is required.");
    }

    [Fact]
    public void Create_accepts_200_char_title_and_rejects_201()
    {
        TaskItem.Create(Id, UserId, new string('a', 200), null, TaskItemStatus.ToDo, null, Now).IsSuccess.ShouldBeTrue();

        var result = TaskItem.Create(Id, UserId, new string('a', 201), null, TaskItemStatus.ToDo, null, Now);
        result.IsFailure.ShouldBeTrue();
        result.Error!.FieldErrors!["title"].ShouldContain("Title must be at most 200 characters.");
    }

    [Fact]
    public void Create_converts_blank_description_to_null_and_rejects_2001_chars()
    {
        TaskItem.Create(Id, UserId, "t", "   ", TaskItemStatus.ToDo, null, Now).Value.Description.ShouldBeNull();
        TaskItem.Create(Id, UserId, "t", new string('d', 2000), TaskItemStatus.ToDo, null, Now).IsSuccess.ShouldBeTrue();

        var result = TaskItem.Create(Id, UserId, "t", new string('d', 2001), TaskItemStatus.ToDo, null, Now);
        result.IsFailure.ShouldBeTrue();
        result.Error!.FieldErrors!["description"].ShouldContain("Description must be at most 2000 characters.");
    }

    [Fact]
    public void Create_accepts_due_date_equal_to_today_utc()
    {
        var now = new DateTimeOffset(2026, 11, 10, 23, 59, 59, TimeSpan.Zero);
        var result = TaskItem.Create(Id, UserId, "t", null, TaskItemStatus.ToDo, new DateOnly(2026, 11, 10), now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DueDate.ShouldBe(new DateOnly(2026, 11, 10));
    }

    [Fact]
    public void Create_rejects_due_date_before_today_utc()
    {
        var now = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
        var result = TaskItem.Create(Id, UserId, "t", null, TaskItemStatus.ToDo, new DateOnly(2026, 11, 9), now);

        result.IsFailure.ShouldBeTrue();
        result.Error!.FieldErrors!["dueDate"].ShouldContain("Due date cannot be in the past.");
    }

    [Fact]
    public void Create_reports_all_field_errors_together()
    {
        var result = TaskItem.Create(Id, UserId, " ", new string('d', 2001), TaskItemStatus.ToDo, new DateOnly(2020, 1, 1), Now);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("validation_failed");
        result.Error.FieldErrors!.Keys.ShouldBe(["title", "description", "dueDate"], ignoreOrder: true);
    }

    [Fact]
    public void Rehydrate_restores_all_fields()
    {
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var updated = created.AddDays(1);
        var task = TaskItem.Rehydrate(Id, UserId, "t", "d", TaskItemStatus.Done, new DateOnly(2026, 2, 2), created, updated);

        task.Status.ShouldBe(TaskItemStatus.Done);
        task.DueDate.ShouldBe(new DateOnly(2026, 2, 2));
        task.CreatedAt.ShouldBe(created);
        task.UpdatedAt.ShouldBe(updated);
    }
}
