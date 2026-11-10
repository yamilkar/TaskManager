using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TaskManager.SharedKernel.Security;

namespace TaskManager.ApiCommon;

public static class JwtBearerSetup
{
    public static void Configure(JwtBearerOptions options, JwtOptions jwt)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
        };

        // Every endpoint keys data on the 'sub' claim. A correctly signed token without a GUID 'sub'
        // is a bad credential, so reject it here as 401 instead of letting CurrentUserId throw a 500.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out _))
                {
                    context.Fail("Token has no valid 'sub' claim.");
                }

                return Task.CompletedTask;
            },
        };
    }
}
