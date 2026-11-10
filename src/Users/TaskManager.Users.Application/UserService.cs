using TaskManager.SharedKernel;
using TaskManager.Users.Application.Contracts;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Application.Validation;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application;

public sealed class UserService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    IClock clock,
    IAppMetrics metrics) : IUserService
{
    public async Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct)
    {
        var invalid = RegisterUserRequestValidator.Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        var email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return email.Error!;
        }

        var user = User.Create(Guid.NewGuid(), request.FullName, email.Value, hasher.Hash(request.Password!), clock.UtcNow);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        if (!await users.TryAddAsync(user.Value, ct))
        {
            return UserErrors.EmailAlreadyRegistered;
        }

        metrics.Increment(UsersMetrics.UserRegistered);
        return ToResponse(user.Value);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var password = request.Password ?? string.Empty;
        var email = Email.Create(request.Email);

        // A malformed email is reported as invalid credentials so callers cannot tell which part was wrong.
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, ct) : null;

        // Always verify (against a dummy hash when the user is unknown) to equalize timing.
        var verified = hasher.Verify(user?.PasswordHash, password);
        if (user is null || !verified)
        {
            metrics.Increment(UsersMetrics.LoginFailed);
            return UserErrors.InvalidCredentials;
        }

        var token = tokens.Issue(user);
        metrics.Increment(UsersMetrics.LoginSucceeded);
        return new AuthResponse(token.AccessToken, token.ExpiresAt, ToResponse(user));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        return user is null ? UserErrors.NotFound : ToResponse(user);
    }

    private static UserResponse ToResponse(User u) => new(u.Id, u.FullName, u.Email.Value, u.CreatedAt);
}
