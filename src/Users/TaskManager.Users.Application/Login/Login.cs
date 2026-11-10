using TaskManager.SharedKernel;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Login;

public sealed record LoginCommand(string? Email, string? Password);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

public sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    IAppMetrics metrics) : ICommandHandler<LoginCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var invalid = LoginValidator.Validate(command);
        if (invalid is not null)
        {
            return invalid;
        }

        var email = Email.Create(command.Email);

        // A malformed email is reported as invalid credentials so callers cannot tell which part was wrong.
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, ct) : null;

        // Always verify (against a dummy hash when the user is unknown) to equalize timing.
        var verified = hasher.Verify(user?.PasswordHash, command.Password!);
        if (user is null || !verified)
        {
            metrics.Increment(UsersMetrics.LoginFailed);
            return UserErrors.InvalidCredentials;
        }

        var token = tokens.Issue(user);
        metrics.Increment(UsersMetrics.LoginSucceeded);
        return new AuthResponse(token.AccessToken, token.ExpiresAt, user.ToResponse());
    }
}
