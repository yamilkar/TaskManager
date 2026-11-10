using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Contracts;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;
using TaskManager.Testing;

namespace TaskManager.Tasks.Application.Tests;

public class TaskServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
    private readonly ITaskRepository _repo = Substitute.For<ITaskRepository>();
    private readonly IAppMetrics _metrics = Substitute.For<IAppMetrics>();
    private readonly Guid _userId = Guid.NewGuid();

    private TaskService Sut => new(_repo, new FixedClock(Now), _metrics);

    [Fact]
    public async Task CreateAsync_persists_task_owned_by_caller_and_returns_response()
    {
        var result = await Sut.CreateAsync(_userId, new CreateTaskRequest(" Write ", "d", null, new DateOnly(2026, 11, 10)), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Write", result.Value.Title);
        Assert.Equal("ToDo", result.Value.Status);
        Assert.Equal(Now, result.Value.CreatedAt);
        await _repo.Received(1).AddAsync(
            Arg.Is<TaskItem>(t => t.UserId == _userId && t.Status == TaskItemStatus.ToDo && t.Id == result.Value.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_parses_status_case_insensitively()
    {
        var result = await Sut.CreateAsync(_userId, new CreateTaskRequest("t", null, "inprogress", null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("InProgress", result.Value.Status);
    }

    [Theory]
    [InlineData("Blocked")]
    [InlineData("ToDo,Done")]
    [InlineData("5")]
    [InlineData("1")]
    [InlineData("")]
    public async Task CreateAsync_rejects_unknown_status_with_field_error(string status)
    {
        var result = await Sut.CreateAsync(_userId, new CreateTaskRequest("t", null, status, null), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(["Status must be one of: ToDo, InProgress, Done."], result.Error.FieldErrors!["status"]);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_combines_status_and_other_field_errors_in_one_error()
    {
        var result = await Sut.CreateAsync(_userId, new CreateTaskRequest(" ", null, "bogus", null), default);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Contains("status", result.Error.FieldErrors!.Keys);
        Assert.Contains("title", result.Error.FieldErrors.Keys);
    }

    [Fact]
    public async Task CreateAsync_returns_validation_error_and_does_not_persist_for_blank_title()
    {
        var result = await Sut.CreateAsync(_userId, new CreateTaskRequest("  ", null, null, null), default);

        Assert.True(result.IsFailure);
        Assert.Contains("title", result.Error!.FieldErrors!.Keys);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
        _metrics.DidNotReceive().Increment(Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_increments_TaskCreated()
    {
        await Sut.CreateAsync(_userId, new CreateTaskRequest("t", null, null, null), default);

        _metrics.Received(1).Increment(TasksMetrics.TaskCreated);
    }

    [Fact]
    public async Task ListForUserAsync_returns_newest_first()
    {
        var older = TaskItem.Rehydrate(Guid.NewGuid(), _userId, "old", null, TaskItemStatus.ToDo, null, Now.AddDays(-2), Now.AddDays(-2));
        var newer = TaskItem.Rehydrate(Guid.NewGuid(), _userId, "new", null, TaskItemStatus.Done, null, Now.AddDays(-1), Now.AddDays(-1));
        _repo.ListByUserAsync(_userId, Arg.Any<CancellationToken>()).Returns([older, newer]);

        var result = await Sut.ListForUserAsync(_userId, default);

        Assert.Equal(["new", "old"], result.Value.Select(t => t.Title));
        Assert.Equal("Done", result.Value[0].Status);
    }

    [Fact]
    public async Task GetByIdAsync_returns_task_for_owner()
    {
        var task = TaskItem.Rehydrate(Guid.NewGuid(), _userId, "t", null, TaskItemStatus.ToDo, null, Now, Now);
        _repo.GetAsync(_userId, task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await Sut.GetByIdAsync(_userId, task.Id, default);

        Assert.Equal(task.Id, result.Value.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_NotFound_when_task_belongs_to_another_user()
    {
        var taskId = Guid.NewGuid();
        _repo.GetAsync(_userId, taskId, Arg.Any<CancellationToken>()).Returns((TaskItem?)null);

        var result = await Sut.GetByIdAsync(_userId, taskId, default);

        Assert.Equal(TaskErrors.NotFound, result.Error);
        await _repo.Received(1).GetAsync(_userId, taskId, Arg.Any<CancellationToken>());
    }
}
