namespace TaskManager.Users.Application.Contracts;

public sealed record LoginRequest(string? Email, string? Password);
