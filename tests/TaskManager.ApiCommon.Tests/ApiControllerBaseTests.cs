using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManager.ApiCommon;
using TaskManager.SharedKernel;

namespace TaskManager.ApiCommon.Tests;

public sealed class ApiControllerBaseTests
{
    private sealed class TestController : ApiControllerBase
    {
        public IActionResult Map(Error error) => Problem(error);

        public Guid UserId => CurrentUserId;
    }

    private static TestController Create(ClaimsPrincipal? user = null) => new()
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) },
        },
    };

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unavailable, 503)]
    public void Problem_maps_error_type_to_status(ErrorType type, int status)
    {
        var error = new Error("some.code", "Some message.", type);

        var result = Create().Map(error).ShouldBeOfType<ObjectResult>();

        result.StatusCode.ShouldBe(status);
        var details = result.Value.ShouldBeAssignableTo<ProblemDetails>()!;
        details.Status.ShouldBe(status);
        details.Detail.ShouldBe("Some message.");
        details.Extensions["code"].ShouldBe("some.code");
    }

    [Fact]
    public void Problem_validation_includes_field_errors_and_code()
    {
        var fields = new Dictionary<string, string[]> { ["email"] = ["Email is invalid."] };
        var error = Error.Validation("validation.failed", "One or more validation errors occurred.", fields);

        var result = Create().Map(error).ShouldBeOfType<ObjectResult>();

        var details = result.Value.ShouldBeOfType<ValidationProblemDetails>();
        details.Errors["email"].ShouldBe(["Email is invalid."]);
        details.Extensions["code"].ShouldBe("validation.failed");
        details.Status.ShouldBe(400);
    }

    [Fact]
    public void CurrentUserId_reads_sub_claim()
    {
        var id = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));

        Create(user).UserId.ShouldBe(id);
    }

    [Fact]
    public void CurrentUserId_throws_when_sub_missing()
    {
        Should.Throw<InvalidOperationException>(() => Create().UserId);
    }
}
