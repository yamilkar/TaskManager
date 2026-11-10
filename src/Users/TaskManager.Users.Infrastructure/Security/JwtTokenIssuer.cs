using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManager.SharedKernel;
using TaskManager.SharedKernel.Security;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Infrastructure.Security;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(User user)
    {
        var jwt = options.Value;
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(jwt.LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
                new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
                new Claim("name", user.FullName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
