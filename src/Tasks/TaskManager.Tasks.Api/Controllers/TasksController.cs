using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.ApiCommon;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application;
using TaskManager.Tasks.Application.CreateTask;
using TaskManager.Tasks.Application.GetTask;
using TaskManager.Tasks.Application.ListTasks;
using TaskManager.Tasks.Application.UpdateTask;

namespace TaskManager.Tasks.Api.Controllers;

/// <summary>Request body for creating a task. The owner is never part of the body.</summary>
public sealed record CreateTaskRequest(string? Title, string? Description, string? Status, DateOnly? DueDate);

/// <summary>Request body for replacing a task's editable fields. Owner, id and timestamps are never part of the body.</summary>
public sealed record UpdateTaskRequest(string? Title, string? Description, string? Status, DateOnly? DueDate);

[Authorize]
[Route("api/v1/tasks")]
public sealed class TasksController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskRequest request,
        [FromServices] ICommandHandler<CreateTaskCommand, TaskResponse> handler,
        CancellationToken ct)
    {
        var command = new CreateTaskCommand(CurrentUserId, request.Title, request.Description, request.Status, request.DueDate);
        var result = await handler.HandleAsync(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : Problem(result.Error!);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListTasksQuery, IReadOnlyList<TaskResponse>> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ListTasksQuery(CurrentUserId), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetTaskQuery, TaskResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetTaskQuery(CurrentUserId, id), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTaskRequest request,
        [FromServices] ICommandHandler<UpdateTaskCommand, TaskResponse> handler,
        CancellationToken ct)
    {
        var command = new UpdateTaskCommand(CurrentUserId, id, request.Title, request.Description, request.Status, request.DueDate);
        var result = await handler.HandleAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);
    }
}
