using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskManager.SharedKernel;
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
    public async Task ListTasks_HandleAsync_noLimit_requestsDefault20AndMapsPage()
    {
        var task = TaskItem.Rehydrate(Guid.NewGuid(), _userId, "t", null, TaskItemStatus.Done, null, Now, Now);
        _repo.ListByUserAsync(_userId, 20, null, Arg.Any<CancellationToken>())
            .Returns(new TaskPageResult([task], "next"));

        var result = await new ListTasksHandler(_repo).HandleAsync(new ListTasksQuery(_userId, null, null), default);

        Assert.Equal(task.Id, result.Value.Items.Single().Id);
        Assert.Equal("Done", result.Value.Items.Single().Status);
        Assert.Equal("next", result.Value.NextCursor);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task ListTasks_HandleAsync_boundaryLimit_passesItThrough(int limit)
    {
        _repo.ListByUserAsync(_userId, limit, "c", Arg.Any<CancellationToken>())
            .Returns(new TaskPageResult([], null));

        var result = await new ListTasksHandler(_repo).HandleAsync(new ListTasksQuery(_userId, limit, "c"), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Null(result.Value.NextCursor);
        await _repo.Received(1).ListByUserAsync(_userId, limit, "c", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public async Task ListTasks_HandleAsync_outOfRangeLimit_returnsLimitFieldErrorWithoutQuerying(int limit)
    {
        var result = await new ListTasksHandler(_repo).HandleAsync(new ListTasksQuery(_userId, limit, null), default);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(["Limit must be between 1 and 100."], result.Error.FieldErrors!["limit"]);
        await _repo.DidNotReceiveWithAnyArgs().ListByUserAsync(default, default, default, default);
    }

    [Fact]
    public async Task ListTasks_HandleAsync_repositoryRejectsCursor_returnsCursorFieldError()
    {
        _repo.ListByUserAsync(_userId, 20, "bad", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidCursorException("bad"));

        var result = await new ListTasksHandler(_repo).HandleAsync(new ListTasksQuery(_userId, null, "bad"), default);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(["Cursor is invalid."], result.Error.FieldErrors!["cursor"]);
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
