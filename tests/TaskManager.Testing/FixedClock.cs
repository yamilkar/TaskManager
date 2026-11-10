using TaskManager.SharedKernel;

namespace TaskManager.Testing;

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
}
