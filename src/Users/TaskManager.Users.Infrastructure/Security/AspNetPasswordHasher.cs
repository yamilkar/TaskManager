using Microsoft.AspNetCore.Identity;
using TaskManager.Users.Application.Ports;

namespace TaskManager.Users.Infrastructure.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();
    private readonly string _dummyHash;

    /// <summary>
    /// Computes the dummy hash eagerly, so the first unknown-email login is not slower than later ones.
    /// Registered as a ready instance, the cost is paid at startup rather than on a request.
    /// </summary>
    public AspNetPasswordHasher()
    {
        _dummyHash = _inner.HashPassword(new object(), Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password) => _inner.HashPassword(new object(), password);

    public bool Verify(string? passwordHash, string password)
    {
        if (passwordHash is null)
        {
            // Equalize timing with the real path; the result is always false.
            _inner.VerifyHashedPassword(new object(), _dummyHash, password);
            return false;
        }

        var result = _inner.VerifyHashedPassword(new object(), passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
