namespace TaskManager.SharedKernel;

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(code, message, ErrorType.Validation, fieldErrors);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
