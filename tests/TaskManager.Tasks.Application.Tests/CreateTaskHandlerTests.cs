using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.CreateTask;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;
using TaskManager.Testing;

namespace TaskManager.Tasks.Application.Tests;

public class CreateTaskHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
    private readonly ITaskRepository _repo = Substitute.For<ITaskRepository>();
    private readonly IAppMetrics _metrics = Substitute.For<IAppMetrics>();
    private readonly Guid _userId = Guid.NewGuid();

    private CreateTaskHandler Sut => new(_repo, new FixedClock(Now), _metrics);

    private CreateTaskCommand Command(string? title, string? status = null, DateOnly? dueDate = null, string? description = null) =>
        new(_userId, title, description, status, dueDate);

    [Fact]
    public async Task HandleAsync_validCommand_persistsTaskOwnedByCaller()
    {
        var result = await Sut.HandleAsync(Command(" Write ", description: "d", dueDate: new DateOnly(2026, 11, 10)), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Write", result.Value.Title);
        Assert.Equal("ToDo", result.Value.Status);
        Assert.Equal(Now, result.Value.CreatedAt);
        await _repo.Received(1).AddAsync(
            Arg.Is<TaskItem>(t => t.UserId == _userId && t.Status == TaskItemStatus.ToDo && t.Id == result.Value.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_lowercaseStatus_parsesCaseInsensitively()
    {
        var result = await Sut.HandleAsync(Command("t", "inprogress"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("InProgress", result.Value.Status);
    }

    [Theory]
    [InlineData("Blocked")]
    [InlineData("ToDo,Done")]
    [InlineData("5")]
    [InlineData("1")]
    [InlineData("")]
    public async Task HandleAsync_unknownStatus_returnsStatusFieldError(string status)
    {
        var result = await Sut.HandleAsync(Command("t", status), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(["Status must be one of: ToDo, InProgress, Done."], result.Error.FieldErrors!["status"]);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_badStatusAndBlankTitle_combinesErrorsInOneValidationError()
    {
        var result = await Sut.HandleAsync(Command(" ", "bogus"), default);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Contains("status", result.Error.FieldErrors!.Keys);
        Assert.Contains("title", result.Error.FieldErrors.Keys);
    }

    [Fact]
    public async Task HandleAsync_blankTitle_returnsValidationErrorAndDoesNotPersist()
    {
        var result = await Sut.HandleAsync(Command("  "), default);

        Assert.True(result.IsFailure);
        Assert.Contains("title", result.Error!.FieldErrors!.Keys);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
        _metrics.DidNotReceive().Increment(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_success_incrementsTaskCreated()
    {
        await Sut.HandleAsync(Command("t"), default);

        _metrics.Received(1).Increment(TasksMetrics.TaskCreated);
    }

    [Fact]
    public async Task HandleAsync_pastDueDate_returnsDueDateErrorAndDoesNotPersist()
    {
        var result = await Sut.HandleAsync(Command("t", dueDate: new DateOnly(2026, 11, 8)), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(TaskItem.DueDateInPastMessage, result.Error.FieldErrors!["dueDate"]);
        await _repo.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_descriptionStatusAndDueDate_areReturnedInResponse()
    {
        var result = await Sut.HandleAsync(Command("t", "Done", new DateOnly(2026, 12, 1), "  notes "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("notes", result.Value.Description);
        Assert.Equal("Done", result.Value.Status);
        Assert.Equal(new DateOnly(2026, 12, 1), result.Value.DueDate);
    }
}
