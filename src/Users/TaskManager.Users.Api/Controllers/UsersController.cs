using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.ApiCommon;
using TaskManager.Users.Application;

namespace TaskManager.Users.Api.Controllers;

[Route("api/v1/users")]
[Authorize]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await users.GetByIdAsync(CurrentUserId, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
