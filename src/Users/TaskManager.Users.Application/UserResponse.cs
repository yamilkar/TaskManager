using TaskManager.Users.Domain;

namespace TaskManager.Users.Application;

public sealed record UserResponse(Guid Id, string FullName, string Email, DateTimeOffset CreatedAt);

internal static class UserMapping
{
    public static UserResponse ToResponse(this User u) => new(u.Id, u.FullName, u.Email.Value, u.CreatedAt);
}
