using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManager.SharedKernel.Security;
using TaskManager.Testing;
using TaskManager.Users.Domain;
using TaskManager.Users.Infrastructure.Security;

namespace TaskManager.Users.Infrastructure.Tests;

public sealed class JwtTokenIssuerTests
{
    private const string Key = "unit-test-signing-key-with-at-least-32-bytes!";

    [Fact]
    public async Task Issue_produces_token_with_spec_claims_issuer_audience_and_60_minute_expiry()
    {
        var now = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        var issuer = new JwtTokenIssuer(Options.Create(new JwtOptions { SigningKey = Key }), new FixedClock(now));
        var user = User.Rehydrate(Guid.NewGuid(), "Ana Perez", Email.Create("ana@x.com").Value, "hash", now);

        var issued = issuer.Issue(user);

        issued.ExpiresAt.ShouldBe(now.AddMinutes(60));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "taskmanager-users",
            ValidAudience = "taskmanager-api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = false,
        });
        result.IsValid.ShouldBeTrue();
        var jwt = (JsonWebToken)result.SecurityToken;
        jwt.Issuer.ShouldBe("taskmanager-users");
        jwt.Audiences.ShouldContain("taskmanager-api");
        jwt.GetClaim("sub").Value.ShouldBe(user.Id.ToString("D"));
        jwt.GetClaim("email").Value.ShouldBe("ana@x.com");
        jwt.GetClaim("name").Value.ShouldBe("Ana Perez");
        jwt.GetClaim("jti").Value.ShouldNotBeNullOrEmpty();
        jwt.ValidTo.ShouldBe(now.AddMinutes(60).UtcDateTime);
    }
}
