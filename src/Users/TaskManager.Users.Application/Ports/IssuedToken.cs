namespace TaskManager.Users.Application.Ports;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
