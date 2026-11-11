using TaskManager.SharedKernel;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Register;

public static class RegisterUserValidator
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public static Error? Validate(RegisterUserCommand c)
    {
        var fields = new Dictionary<string, string[]>();

        var nameProblem = User.ValidateFullName(c.FullName);
        if (nameProblem is not null)
        {
            fields["fullName"] = [nameProblem];
        }

        if (Email.Create(c.Email).IsFailure)
        {
            fields["email"] = [Email.InvalidMessage];
        }

        var password = c.Password;
        if (string.IsNullOrEmpty(password))
        {
            fields["password"] = ["Password is required."];
        }
        else
        {
            var problems = PasswordProblems(password);
            if (problems.Count > 0)
            {
                fields["password"] = [.. problems];
            }
        }

        return fields.Count == 0
            ? null
            : Error.Validation("validation_failed", "One or more validation errors occurred.", fields);
    }

    /// <summary>The password rule for every account, also applied to the deployed demo user's password.</summary>
    public static IReadOnlyList<string> PasswordProblems(string password)
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

        return problems;
    }
}
