namespace TaskManager.SharedKernel;

/// <summary>
/// Handles one use case that changes state. Controllers inject the handler they need directly;
/// there is deliberately no mediator or dispatcher in between.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct);
}
