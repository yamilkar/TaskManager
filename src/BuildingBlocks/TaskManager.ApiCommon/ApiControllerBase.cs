using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManager.SharedKernel;

namespace TaskManager.ApiCommon;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated user has no valid 'sub' claim.");

    protected IActionResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        ProblemDetails details = error.Type == ErrorType.Validation
            ? new ValidationProblemDetails(
                (error.FieldErrors ?? new Dictionary<string, string[]>())
                    .ToDictionary(kv => kv.Key, kv => kv.Value))
            : new ProblemDetails();

        details.Status = status;
        details.Title = error.Type switch
        {
            ErrorType.Validation => "One or more validation errors occurred.",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            _ => "Error",
        };
        details.Detail = error.Message;
        details.Extensions["code"] = error.Code;

        return new ObjectResult(details)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
