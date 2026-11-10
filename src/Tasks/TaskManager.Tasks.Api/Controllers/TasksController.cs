using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.ApiCommon;
using TaskManager.Tasks.Application;
using TaskManager.Tasks.Application.Contracts;

namespace TaskManager.Tasks.Api.Controllers;

[Authorize]
[Route("api/v1/tasks")]
public sealed class TasksController(ITaskService tasks) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken ct)
    {
        var result = await tasks.CreateAsync(CurrentUserId, request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : Problem(result.Error!);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await tasks.ListForUserAsync(CurrentUserId, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await tasks.GetByIdAsync(CurrentUserId, id, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
