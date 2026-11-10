using TaskManager.SharedKernel;
using TaskManager.Users.Application.Contracts;

namespace TaskManager.Users.Application;

public interface IUserService
{
    Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct);
}
