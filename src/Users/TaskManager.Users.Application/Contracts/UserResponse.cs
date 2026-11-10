namespace TaskManager.Users.Application.Contracts;

public sealed record UserResponse(Guid Id, string FullName, string Email, DateTimeOffset CreatedAt);
