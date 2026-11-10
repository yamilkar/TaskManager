using System.Globalization;
using Amazon.DynamoDBv2.Model;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Infrastructure.Persistence;

public static class TaskItemMapper
{
    public const string CreatedAtIdAttribute = "createdAtId";

    private const string DateFormat = "yyyy-MM-dd";

    public static string CreatedAtId(TaskItem task) =>
        task.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) + "#" + task.Id.ToString("D");

    public static Dictionary<string, AttributeValue> ToItem(TaskItem task)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["userId"] = task.UserId.ToAttr(),
            ["_id"] = task.Id.ToAttr(),
            ["title"] = task.Title.ToAttr(),
            ["status"] = task.Status.ToString().ToAttr(),
            ["createdAt"] = task.CreatedAt.ToAttr(),
            ["updatedAt"] = task.UpdatedAt.ToAttr(),
            [CreatedAtIdAttribute] = CreatedAtId(task).ToAttr(),
        };

        if (task.Description is not null)
        {
            item["description"] = task.Description.ToAttr();
        }

        if (task.DueDate is { } due)
        {
            item["due_date"] = due.ToString(DateFormat, CultureInfo.InvariantCulture).ToAttr();
        }

        return item;
    }

    public static TaskItem FromItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var status = Enum.Parse<TaskItemStatus>(item.GetString("status"));
        var due = item.GetStringOrNull("due_date");

        return TaskItem.Rehydrate(
            item.GetGuid("_id"),
            item.GetGuid("userId"),
            item.GetString("title"),
            item.GetStringOrNull("description"),
            status,
            due is null ? null : DateOnly.ParseExact(due, DateFormat, CultureInfo.InvariantCulture),
            item.GetDateTimeOffset("createdAt"),
            item.GetDateTimeOffset("updatedAt"));
    }
}
