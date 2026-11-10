using NSubstitute;
using TaskManager.Tasks.Application.GetTask;
using TaskManager.Tasks.Application.ListTasks;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Application.Tests;

public class TaskQueryHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
    private readonly ITaskRepository _repo = Substitute.For<ITaskRepository>();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task ListTasks_HandleAsync_multipleTasks_returnsNewestFirst()
    {
        var older = TaskItem.Rehydrate(Guid.NewGuid(), _userId, "old", null, TaskItemStatus.ToDo, null, Now.AddDays(-2), Now.AddDays(-2));
        var newer = TaskItem.Rehydrate(Guid.NewGuid(), _userId, "new", null, TaskItemStatus.Done, null, Now.AddDays(-1), Now.AddDays(-1));
        _repo.ListByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns([older, newer]);

        var result = await new ListTasksHandler(_repo).HandleAsync(new ListTasksQuery(_userId), default);

        Assert.Equal(["new", "old"], result.Value.Select(t => t.Title));
        Assert.Equal("Done", result.Value[0].Status);
    }

    [Fact]
    public async Task GetTask_HandleAsync_ownTask_returnsTask()
    {
        var task = TaskItem.Rehydrate(Guid.NewGuid(), _userId, "t", null, TaskItemStatus.ToDo, null, Now, Now);
        _repo.GetAsync(_userId, task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await new GetTaskHandler(_repo).HandleAsync(new GetTaskQuery(_userId, task.Id), default);

        Assert.Equal(task.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetTask_HandleAsync_anotherUsersTask_returnsNotFound()
    {
        var taskId = Guid.NewGuid();
        _repo.GetAsync(_userId, taskId, Arg.Any<CancellationToken>()).Returns((TaskItem?)null);

        var result = await new GetTaskHandler(_repo).HandleAsync(new GetTaskQuery(_userId, taskId), default);

        Assert.Equal(TaskErrors.NotFound, result.Error);
        await _repo.Received(1).GetAsync(_userId, taskId, Arg.Any<CancellationToken>());
    }
}
