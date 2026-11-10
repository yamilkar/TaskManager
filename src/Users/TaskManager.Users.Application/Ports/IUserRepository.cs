using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Ports;

public interface IUserRepository
{
    /// <summary>Returns false when the email is already registered.</summary>
    Task<bool> TryAddAsync(User user, CancellationToken ct);

    Task<User?> GetByEmailAsync(Email email, CancellationToken ct);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
}
