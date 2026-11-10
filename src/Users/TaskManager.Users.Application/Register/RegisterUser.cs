using TaskManager.SharedKernel;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Register;

public sealed record RegisterUserCommand(string? FullName, string? Email, string? Password);

public sealed class RegisterUserHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IClock clock,
    IAppMetrics metrics) : ICommandHandler<RegisterUserCommand, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(RegisterUserCommand command, CancellationToken ct)
    {
        var invalid = RegisterUserValidator.Validate(command);
        if (invalid is not null)
        {
            return invalid;
        }

        var email = Email.Create(command.Email);
        if (email.IsFailure)
        {
            return email.Error!;
        }

        var user = User.Create(Guid.NewGuid(), command.FullName, email.Value, hasher.Hash(command.Password!), clock.UtcNow);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        switch (await users.TryAddAsync(user.Value, ct))
        {
            case AddUserResult.EmailTaken:
                return UserErrors.EmailAlreadyRegistered;
            case AddUserResult.Busy:
                return UserErrors.RegistrationBusy;
        }

        metrics.Increment(UsersMetrics.UserRegistered);
        return user.Value.ToResponse();
    }
}
