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
using TaskManager.Tasks.Application.UpdateTask;
using TaskManager.Tasks.Application.DeleteTask;
using TaskManager.Tasks.Domain;
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
    public async Task Create_without_title_returns_400_with_title_error()
    {
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await ClientWith(factory, Guid.NewGuid()).PostAsync(Url, Json("""{"description":"d"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.GetProperty("title")[0].GetString().ShouldBe("Title is required.");
    }

    [Fact]
    public async Task Create_without_status_returns_201_with_status_ToDo()
    {
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await ClientWith(factory, Guid.NewGuid()).PostAsync(Url, Json("""{"title":"x"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().ShouldBe("ToDo");
    }

    [Fact]
    public async Task Create_with_empty_status_returns_400_with_status_error()
    {
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await ClientWith(factory, Guid.NewGuid()).PostAsync(Url, Json("""{"title":"x","status":""}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("status", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_with_past_due_date_returns_400_with_reason()
    {
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await ClientWith(factory, Guid.NewGuid()).PostAsync(Url, Json("""{"title":"x","dueDate":"2000-01-01"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.GetProperty("dueDate")[0].GetString().ShouldBe(TaskItem.DueDateInPastMessage);
    }

    [Fact]
    public async Task Create_with_description_status_and_due_date_returns_201_with_all_fields()
    {
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await ClientWith(factory, Guid.NewGuid()).PostAsync(Url, Json("""{"title":"x","description":"d","status":"InProgress","dueDate":"2099-12-31"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().ShouldBe("d");
        body.GetProperty("status").GetString().ShouldBe("InProgress");
        body.GetProperty("dueDate").GetString().ShouldBe("2099-12-31");
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

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task List_with_signed_token_lacking_valid_sub_returns_401_not_500(string? subject)
    {
        using var factory = new TasksApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.CreateWithSubject(subject, factory.SigningKey));

        var response = await client.GetAsync(Url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await factory.List.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
    }

    [Fact]
    public async Task Token_signed_with_different_key_returns_401()
    {
        using var factory = new TasksApiFactory();

        var response = await ClientWith(factory, Guid.NewGuid(), "a-completely-different-signing-key-0123456789")
            .GetAsync(Url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, object id, string json) =>
        client.PutAsync($"{Url}/{id}", Json(json));

    private static DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    private static TasksApiFactory RealFactoryWithTask(Guid userId, Guid taskId, DateOnly? due = null)
    {
        var factory = new TasksApiFactory(useRealHandlers: true);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-20);
        factory.Repository.GetAsync(userId, taskId, Arg.Any<CancellationToken>())
            .Returns(TaskItem.Rehydrate(taskId, userId, "Old", "old", TaskItemStatus.ToDo, due, createdAt, createdAt));
        factory.Repository.UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>()).Returns(true);
        return factory;
    }

    [Fact]
    public async Task Update_without_token_returns_401()
    {
        using var factory = new TasksApiFactory();

        var response = await Put(ClientWith(factory, null), Guid.NewGuid(), """{"title":"x","status":"ToDo"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_with_token_passes_sub_route_id_and_body_to_handler()
    {
        using var factory = new TasksApiFactory();
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        factory.Update.HandleAsync(Arg.Any<UpdateTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaskResponse>.Success(Sample(taskId, "New")));

        var response = await Put(ClientWith(factory, userId), taskId,
            """{"title":"New","description":"d","status":"Done","dueDate":"2026-12-01"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString().ShouldBe("New");
        await factory.Update.Received(1).HandleAsync(
            Arg.Is<UpdateTaskCommand>(c => c.UserId == userId && c.TaskId == taskId && c.Title == "New"
                && c.Description == "d" && c.Status == "Done" && c.DueDate == new DateOnly(2026, 12, 1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ignores_userId_id_and_createdAt_in_body()
    {
        using var factory = new TasksApiFactory();
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var other = Guid.NewGuid();
        factory.Update.HandleAsync(Arg.Any<UpdateTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaskResponse>.Success(Sample(taskId)));

        var response = await Put(ClientWith(factory, userId), taskId,
            $$"""{"title":"x","status":"ToDo","userId":"{{other}}","id":"{{other}}","createdAt":"2020-01-01T00:00:00Z"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await factory.Update.Received(1).HandleAsync(
            Arg.Is<UpdateTaskCommand>(c => c.UserId == userId && c.TaskId == taskId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_with_invalid_due_date_format_returns_400()
    {
        using var factory = new TasksApiFactory();

        var response = await Put(ClientWith(factory, Guid.NewGuid()), Guid.NewGuid(),
            """{"title":"x","status":"ToDo","dueDate":"10/11/2026"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await factory.Update.DidNotReceive().HandleAsync(Arg.Any<UpdateTaskCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_with_non_guid_id_returns_404()
    {
        using var factory = new TasksApiFactory();

        var response = await Put(ClientWith(factory, Guid.NewGuid()), "abc", """{"title":"x","status":"ToDo"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await factory.Update.DidNotReceive().HandleAsync(Arg.Any<UpdateTaskCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_valid_body_returns_200_with_changes()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        using var factory = RealFactoryWithTask(userId, taskId);
        var due = TodayUtc.AddDays(5).ToString("yyyy-MM-dd");

        var response = await Put(ClientWith(factory, userId), taskId,
            $$"""{"title":"New","description":"new d","status":"ToDo","dueDate":"{{due}}"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().ShouldBe(taskId);
        body.GetProperty("title").GetString().ShouldBe("New");
        body.GetProperty("description").GetString().ShouldBe("new d");
        body.GetProperty("dueDate").GetString().ShouldBe(due);
    }

    [Fact]
    public async Task Update_status_only_change_returns_200()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        using var factory = RealFactoryWithTask(userId, taskId);

        var response = await Put(ClientWith(factory, userId), taskId,
            """{"title":"Old","description":"old","status":"InProgress","dueDate":null}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().ShouldBe("InProgress");
    }

    [Fact]
    public async Task Update_unknown_task_returns_404_problem()
    {
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await Put(ClientWith(factory, Guid.NewGuid()), Guid.NewGuid(), """{"title":"x","status":"ToDo"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("tasks.not_found");
        await factory.Repository.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_blank_title_returns_400_with_title_error()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        using var factory = RealFactoryWithTask(userId, taskId);

        var response = await Put(ClientWith(factory, userId), taskId, """{"title":"  ","status":"ToDo"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("title")[0].GetString().ShouldBe("Title is required.");
    }

    [Fact]
    public async Task Update_without_status_returns_400_with_status_error()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        using var factory = RealFactoryWithTask(userId, taskId);

        var response = await Put(ClientWith(factory, userId), taskId, """{"title":"x"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("status")[0].GetString()
            .ShouldBe("Status must be one of: ToDo, InProgress, Done.");
    }

    [Fact]
    public async Task Update_unchanged_past_due_date_returns_200()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var past = TodayUtc.AddDays(-10);
        using var factory = RealFactoryWithTask(userId, taskId, past);

        var response = await Put(ClientWith(factory, userId), taskId,
            $$"""{"title":"Old","status":"Done","dueDate":"{{past:yyyy-MM-dd}}"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_new_past_due_date_returns_400()
    {
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        using var factory = RealFactoryWithTask(userId, taskId);

        var response = await Put(ClientWith(factory, userId), taskId,
            $$"""{"title":"Old","status":"ToDo","dueDate":"{{TodayUtc.AddDays(-10):yyyy-MM-dd}}"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("dueDate", out _).ShouldBeTrue();
        await factory.Repository.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_with_token_passes_sub_and_route_id_and_returns_204()
    {
        using var factory = new TasksApiFactory();
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        factory.Delete.HandleAsync(Arg.Any<DeleteTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(default));

        var response = await ClientWith(factory, userId).DeleteAsync($"{Url}/{taskId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.Delete.Received(1).HandleAsync(
            Arg.Is<DeleteTaskCommand>(c => c.UserId == userId && c.TaskId == taskId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_missing_task_returns_404_problem()
    {
        // Real handler over a substituted repository whose DeleteAsync defaults to false.
        using var factory = new TasksApiFactory(useRealHandlers: true);

        var response = await ClientWith(factory, Guid.NewGuid()).DeleteAsync($"{Url}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }
}
