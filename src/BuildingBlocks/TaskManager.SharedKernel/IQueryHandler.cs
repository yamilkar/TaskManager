namespace TaskManager.SharedKernel;

/// <summary>Handles one read-only use case. It must not change state.</summary>
public interface IQueryHandler<in TQuery, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct);
}
