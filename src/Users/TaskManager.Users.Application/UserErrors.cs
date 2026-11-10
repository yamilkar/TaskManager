using TaskManager.SharedKernel;

namespace TaskManager.Users.Application;

public static class UserErrors
{
    public static readonly Error EmailAlreadyRegistered =
        Error.Conflict("users.email_taken", "An account with this email already exists.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Invalid email or password.");

    public static readonly Error NotFound = Error.NotFound("users.not_found", "User not found.");
}
