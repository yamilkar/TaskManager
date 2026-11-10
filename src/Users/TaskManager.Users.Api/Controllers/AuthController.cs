using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.ApiCommon;
using TaskManager.Users.Application;
using TaskManager.Users.Application.Contracts;
using TaskManager.Users.Application.Validation;

namespace TaskManager.Users.Api.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController(IUserService users) : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken ct)
    {
        var result = await users.RegisterAsync(request, ct);
        return result.IsSuccess
            ? Created("/api/v1/users/me", result.Value)
            : Problem(result.Error!);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var invalid = LoginRequestValidator.Validate(request);
        if (invalid is not null)
        {
            return Problem(invalid);
        }

        var result = await users.LoginAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
