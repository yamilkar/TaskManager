namespace TaskManager.Users.Application.Contracts;

public sealed record RegisterUserRequest(string? FullName, string? Email, string? Password);
