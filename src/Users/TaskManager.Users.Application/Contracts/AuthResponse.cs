namespace TaskManager.Users.Application.Contracts;

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
