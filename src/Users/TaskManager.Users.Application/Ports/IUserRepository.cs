using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Ports;

public interface IUserRepository
{
    /// <summary>Writes the user and its email guard atomically. Never throws for a taken email or a transient conflict.</summary>
    Task<AddUserResult> TryAddAsync(User user, CancellationToken ct);

    Task<User?> GetByEmailAsync(Email email, CancellationToken ct);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
}
