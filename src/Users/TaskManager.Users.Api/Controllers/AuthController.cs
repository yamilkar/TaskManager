using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.ApiCommon;
using TaskManager.SharedKernel;
using TaskManager.Users.Application;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Register;

namespace TaskManager.Users.Api.Controllers;

/// <summary>Request body for registration. Part of the public contract, separate from the Application command.</summary>
public sealed record RegisterRequest(string? FullName, string? Email, string? Password);

/// <summary>Request body for login. Part of the public contract, separate from the Application command.</summary>
public sealed record LoginRequest(string? Email, string? Password);

[Route("api/v1/auth")]
[RequireClientIp]
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] ICommandHandler<RegisterUserCommand, UserResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RegisterUserCommand(request.FullName, request.Email, request.Password), ct);
        return result.IsSuccess
            ? Created("/api/v1/users/me", result.Value)
            : Problem(result.Error!);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, AuthResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
