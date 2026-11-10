namespace TaskManager.SharedKernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
