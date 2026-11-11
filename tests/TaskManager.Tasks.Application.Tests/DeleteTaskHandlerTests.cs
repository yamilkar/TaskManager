using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.DeleteTask;
using TaskManager.Tasks.Application.Ports;

namespace TaskManager.Tasks.Application.Tests;

public class DeleteTaskHandlerTests
{
    private readonly ITaskRepository _repo = Substitute.For<ITaskRepository>();
    private readonly IAppMetrics _metrics = Substitute.For<IAppMetrics>();
    private readonly DeleteTaskCommand _command = new(Guid.NewGuid(), Guid.NewGuid());

    private DeleteTaskHandler Sut => new(_repo, _metrics);

    [Fact]
    public async Task HandleAsync_existingTask_deletesWithCallerIdAndCountsMetric()
    {
        _repo.DeleteAsync(_command.UserId, _command.TaskId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Sut.HandleAsync(_command, default);

        Assert.True(result.IsSuccess);
        _metrics.Received(1).Increment(TasksMetrics.TaskDeleted);
    }

    [Fact]
    public async Task HandleAsync_nothingDeleted_returnsNotFound()
    {
        var result = await Sut.HandleAsync(_command, default);

        Assert.Equal(TaskErrors.NotFound, result.Error);
        _metrics.DidNotReceive().Increment(Arg.Any<string>());
    }
}
