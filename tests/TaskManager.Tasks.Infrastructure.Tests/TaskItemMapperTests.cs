using TaskManager.Tasks.Domain;
using TaskManager.Tasks.Infrastructure.Persistence;

namespace TaskManager.Tasks.Infrastructure.Tests;

public sealed class TaskItemMapperTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 1, 3, 3, 4, 5, TimeSpan.Zero);

    private static TaskItem NewTask(string? description, DateOnly? due, TaskItemStatus status = TaskItemStatus.ToDo) =>
        TaskItem.Rehydrate(Guid.NewGuid(), Guid.NewGuid(), "Title", description, status, due, Created, Updated);

    [Fact]
    public void ToItem_uses_spec_attribute_names_and_omits_null_optional_fields()
    {
        var task = NewTask(null, null);
        var item = TaskItemMapper.ToItem(task);

        item.Keys.OrderBy(k => k).ShouldBe(new[] { "_id", "createdAt", "createdAtId", "status", "title", "updatedAt", "userId" });
        item["userId"].S.ShouldBe(task.UserId.ToString("D"));
        item["_id"].S.ShouldBe(task.Id.ToString("D"));

        var full = TaskItemMapper.ToItem(NewTask("desc", new DateOnly(2026, 3, 9), TaskItemStatus.InProgress));
        full["description"].S.ShouldBe("desc");
        full["due_date"].S.ShouldBe("2026-03-09");
        full["status"].S.ShouldBe("InProgress");
    }

    [Fact]
    public void FromItem_round_trips_including_due_date_and_status()
    {
        var task = NewTask("desc", new DateOnly(2026, 3, 9), TaskItemStatus.Done);
        var back = TaskItemMapper.FromItem(TaskItemMapper.ToItem(task));

        back.Id.ShouldBe(task.Id);
        back.UserId.ShouldBe(task.UserId);
        back.Title.ShouldBe("Title");
        back.Description.ShouldBe("desc");
        back.Status.ShouldBe(TaskItemStatus.Done);
        back.DueDate.ShouldBe(new DateOnly(2026, 3, 9));
        back.CreatedAt.ShouldBe(Created);
        back.UpdatedAt.ShouldBe(Updated);

        var bare = TaskItemMapper.FromItem(TaskItemMapper.ToItem(NewTask(null, null)));
        bare.Description.ShouldBeNull();
        bare.DueDate.ShouldBeNull();
    }

    [Fact]
    public void ToItem_anyTask_writesCreatedAtIdAsUtcRoundTripAndId()
    {
        var id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var created = new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.FromHours(2));
        var task = TaskItem.Rehydrate(id, Guid.NewGuid(), "t", null, TaskItemStatus.ToDo, null, created, created);

        TaskItemMapper.ToItem(task)["createdAtId"].S
            .ShouldBe("2026-11-10T07:30:00.0000000+00:00#aaaaaaaa-0000-0000-0000-000000000001");
    }
}
