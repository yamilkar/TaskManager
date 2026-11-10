namespace TaskManager.Users.Application.Ports;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// When <paramref name="passwordHash"/> is null, verifies against an internal dummy hash and returns false,
    /// which equalizes timing.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}
