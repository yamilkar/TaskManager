using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.ApiCommon;
using TaskManager.SharedKernel;
using TaskManager.Users.Application;
using TaskManager.Users.Application.GetCurrentUser;

namespace TaskManager.Users.Api.Controllers;

[Route("api/v1/users")]
[Authorize]
public sealed class UsersController : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(
        [FromServices] IQueryHandler<GetCurrentUserQuery, UserResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetCurrentUserQuery(CurrentUserId), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
