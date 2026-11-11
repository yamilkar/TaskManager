using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Application.UpdateTask;
using TaskManager.Tasks.Domain;
using TaskManager.Testing;

namespace TaskManager.Tasks.Application.Tests;

public class UpdateTaskHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-11-01T09:00:00Z");
    private readonly ITaskRepository _repo = Substitute.For<ITaskRepository>();
    private readonly IAppMetrics _metrics = Substitute.For<IAppMetrics>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _taskId = Guid.NewGuid();

    private UpdateTaskHandler Sut => new(_repo, new FixedClock(Now), _metrics);

    private UpdateTaskCommand Command(string? title = "New", string? status = "Done") =>
        new(_userId, _taskId, title, "d", status, new DateOnly(2026, 11, 20));

    private void GivenExisting() =>
        _repo.GetAsync(_userId, _taskId, Arg.Any<CancellationToken>())
            .Returns(TaskItem.Rehydrate(_taskId, _userId, "Old", null, TaskItemStatus.ToDo, null, CreatedAt, CreatedAt));

    [Fact]
    public async Task HandleAsync_validCommand_savesUpdatedTaskAndReturnsResponse()
    {
        GivenExisting();
        _repo.UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Sut.HandleAsync(Command(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value.Title);
        Assert.Equal("Done", result.Value.Status);
        Assert.Equal(CreatedAt, result.Value.CreatedAt);
        Assert.Equal(Now, result.Value.UpdatedAt);
        await _repo.Received(1).UpdateAsync(
            Arg.Is<TaskItem>(t => t.Id == _taskId && t.UserId == _userId && t.Title == "New"), Arg.Any<CancellationToken>());
        _metrics.Received(1).Increment(TasksMetrics.TaskUpdated);
    }

    [Fact]
    public async Task HandleAsync_taskNotFound_returnsNotFoundAndDoesNotWrite()
    {
        var result = await Sut.HandleAsync(Command(), default);

        Assert.Equal(TaskErrors.NotFound, result.Error);
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
        _metrics.DidNotReceive().Increment(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_readsWithCallerId_soOtherUsersTaskIsNotFound()
    {
        await Sut.HandleAsync(Command(), default);

        await _repo.Received(1).GetAsync(_userId, _taskId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_invalidFields_returnsValidationErrorAndDoesNotWrite()
    {
        GivenExisting();

        var result = await Sut.HandleAsync(Command(title: " "), default);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.True(result.Error.FieldErrors!.ContainsKey("title"));
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_taskDeletedBeforeWrite_returnsNotFound()
    {
        GivenExisting();
        _repo.UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Sut.HandleAsync(Command(), default);

        Assert.Equal(TaskErrors.NotFound, result.Error);
        _metrics.DidNotReceive().Increment(Arg.Any<string>());
    }
}
