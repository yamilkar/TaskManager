using TaskManager.SharedKernel;
using TaskManager.Users.Application.Contracts;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Validation;

public static class RegisterUserRequestValidator
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public static Error? Validate(RegisterUserRequest r)
    {
        var fields = new Dictionary<string, string[]>();

        var nameProblem = User.ValidateFullName(r.FullName);
        if (nameProblem is not null)
        {
            fields["fullName"] = [nameProblem];
        }

        if (Email.Create(r.Email).IsFailure)
        {
            fields["email"] = [Email.InvalidMessage];
        }

        var password = r.Password;
        if (string.IsNullOrEmpty(password))
        {
            fields["password"] = ["Password is required."];
        }
        else
        {
            var problems = new List<string>();
            if (password.Length < PasswordMinLength)
            {
                problems.Add("Password must be at least 8 characters.");
            }

            if (password.Length > PasswordMaxLength)
            {
                problems.Add("Password must be at most 128 characters.");
            }

            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                problems.Add("Password must contain at least one letter and one digit.");
            }

            if (problems.Count > 0)
            {
                fields["password"] = [.. problems];
            }
        }

        return fields.Count == 0
            ? null
            : Error.Validation("validation_failed", "One or more validation errors occurred.", fields);
    }
}
