using Microsoft.AspNetCore.Identity;
using TaskManager.Users.Application.Ports;

namespace TaskManager.Users.Infrastructure.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();
    private readonly Lazy<string> _dummyHash;

    public AspNetPasswordHasher()
    {
        _dummyHash = new Lazy<string>(() => _inner.HashPassword(new object(), Guid.NewGuid().ToString("N")));
    }

    public string Hash(string password) => _inner.HashPassword(new object(), password);

    public bool Verify(string? passwordHash, string password)
    {
        if (passwordHash is null)
        {
            // Equalize timing with the real path; the result is always false.
            _inner.VerifyHashedPassword(new object(), _dummyHash.Value, password);
            return false;
        }

        var result = _inner.VerifyHashedPassword(new object(), passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
