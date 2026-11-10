using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NSubstitute;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application;
using TaskManager.Tasks.Application.CreateTask;
using TaskManager.Tasks.Application.GetTask;
using TaskManager.Tasks.Application.ListTasks;
using TaskManager.Testing;

namespace TaskManager.Tasks.Api.Tests;

public sealed class TasksEndpointsTests
{
    private const string Url = "/api/v1/tasks";

    private static HttpClient ClientWith(TasksApiFactory factory, Guid? userId, string? key = null)
    {
        var client = factory.CreateClient();
        if (userId is { } id)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", TestJwt.Create(id, key ?? factory.SigningKey));
        }

        return client;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static TaskResponse Sample(Guid id, string title = "x") =>
        new(id, title, null, "ToDo", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Create_without_token_returns_401()
    {
        using var factory = new TasksApiFactory();

        var response = await ClientWith(factory, null).PostAsJsonAsync(Url, new { title = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_with_token_returns_201_location_and_uses_sub_as_owner()
    {
        using var factory = new TasksApiFactory();
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        factory.Create.HandleAsync(Arg.Is<CreateTaskCommand>(c => c.UserId == userId), Arg.Any<CancellationToken>())
            .Returns(Result<TaskResponse>.Success(Sample(taskId, "Write docs")));

        var response = await ClientWith(factory, userId).PostAsJsonAsync(Url, new { title = "Write docs" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.ShouldBe($"{Url}/{taskId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().ShouldBe(taskId);
        await factory.Create.Received(1).HandleAsync(
            Arg.Is<CreateTaskCommand>(c => c.UserId == userId && c.Title == "Write docs"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ignores_userId_in_body()
    {
        using var factory = new TasksApiFactory();
        var userId = Guid.NewGuid();
        var other = Guid.NewGuid();
        factory.Create.HandleAsync(Arg.Any<CreateTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaskResponse>.Success(Sample(Guid.NewGuid())));

        var response = await ClientWith(factory, userId)
            .PostAsync(Url, Json($$"""{"title":"x","userId":"{{other}}"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await factory.Create.Received(1).HandleAsync(
            Arg.Is<CreateTaskCommand>(c => c.UserId == userId), Arg.Any<CancellationToken>());
        await factory.Create.DidNotReceive().HandleAsync(
            Arg.Is<CreateTaskCommand>(c => c.UserId == other), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_with_unknown_status_returns_400_validation_problem()
    {
        // Real handlers over a substituted repository: the status check lives in the handler,
        // so this keeps the test honest about the handler-error to HTTP mapping.
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await ClientWith(factory, Guid.NewGuid())
            .PostAsync(Url, Json("""{"title":"x","status":"Blocked"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("status", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_with_malformed_json_returns_400_problem()
    {
        using var factory = new TasksApiFactory();

        var response = await ClientWith(factory, Guid.NewGuid()).PostAsync(Url, Json("{ not json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Create_with_invalid_due_date_format_returns_400()
    {
        using var factory = new TasksApiFactory();

        var response = await ClientWith(factory, Guid.NewGuid())
            .PostAsync(Url, Json("""{"title":"x","dueDate":"10/11/2026"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await factory.Create.DidNotReceive().HandleAsync(Arg.Any<CreateTaskCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_returns_200_with_tasks_for_caller()
    {
        using var factory = new TasksApiFactory();
        var userId = Guid.NewGuid();
        IReadOnlyList<TaskResponse> items = [Sample(Guid.NewGuid(), "a"), Sample(Guid.NewGuid(), "b")];
        factory.List.HandleAsync(new ListTasksQuery(userId), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TaskResponse>>.Success(items));

        var response = await ClientWith(factory, userId).GetAsync(Url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task GetById_returns_200_for_own_task()
    {
        using var factory = new TasksApiFactory();
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        factory.Get.HandleAsync(new GetTaskQuery(userId, taskId), Arg.Any<CancellationToken>())
            .Returns(Result<TaskResponse>.Success(Sample(taskId)));

        var response = await ClientWith(factory, userId).GetAsync($"{Url}/{taskId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_for_other_users_task_returns_404()
    {
        using var factory = new TasksApiFactory();
        var caller = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        factory.Get.HandleAsync(new GetTaskQuery(caller, taskId), Arg.Any<CancellationToken>())
            .Returns(Result<TaskResponse>.Failure(TaskErrors.NotFound));

        var response = await ClientWith(factory, caller).GetAsync($"{Url}/{taskId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await factory.Get.Received(1).HandleAsync(new GetTaskQuery(caller, taskId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_signed_with_different_key_returns_401()
    {
        using var factory = new TasksApiFactory();

        var response = await ClientWith(factory, Guid.NewGuid(), "a-completely-different-signing-key-0123456789")
            .GetAsync(Url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
