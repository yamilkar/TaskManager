using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TaskManager.Testing;

public static class TestJwt
{
    public static string Create(
        Guid userId,
        string signingKey,
        DateTimeOffset? expires = null,
        string issuer = "taskmanager-users",
        string audience = "taskmanager-api")
    {
        var now = DateTime.UtcNow;
        var expiry = (expires ?? DateTimeOffset.UtcNow.AddMinutes(60)).UtcDateTime;
        // NotBefore must precede Expires so already-expired tokens can be built for tests.
        var notBefore = expiry < now ? expiry.AddMinutes(-1) : now;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString("D")),
                new Claim("email", "test@taskmanager.dev"),
                new Claim("name", "Test User"),
            ]),
            NotBefore = notBefore,
            IssuedAt = notBefore,
            Expires = expiry,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
