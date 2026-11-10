using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.ApiCommon;
using TaskManager.SharedKernel;
using TaskManager.Users.Application;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Register;

namespace TaskManager.Users.Api.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserCommand command,
        [FromServices] ICommandHandler<RegisterUserCommand, UserResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.IsSuccess
            ? Created("/api/v1/users/me", result.Value)
            : Problem(result.Error!);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        [FromServices] ICommandHandler<LoginCommand, AuthResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
