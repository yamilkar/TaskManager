using System.Text.RegularExpressions;
using TaskManager.SharedKernel;

namespace TaskManager.Users.Domain;

public sealed partial record Email
{
    public const int MaxLength = 254;
    public const string ErrorCode = "users.email_invalid";
    public const string InvalidMessage = "Enter a valid email address.";

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxLength || !FormatRegex().IsMatch(normalized))
        {
            return Error.Validation(
                ErrorCode,
                InvalidMessage,
                new Dictionary<string, string[]> { ["email"] = [InvalidMessage] });
        }

        return new Email(normalized);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatRegex();
}
