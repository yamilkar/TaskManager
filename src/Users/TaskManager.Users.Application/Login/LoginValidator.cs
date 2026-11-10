using TaskManager.SharedKernel;

namespace TaskManager.Users.Application.Login;

public static class LoginValidator
{
    public static Error? Validate(LoginCommand c)
    {
        var fields = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(c.Email))
        {
            fields["email"] = ["Email is required."];
        }

        if (string.IsNullOrEmpty(c.Password))
        {
            fields["password"] = ["Password is required."];
        }

        return fields.Count == 0
            ? null
            : Error.Validation("validation_failed", "One or more validation errors occurred.", fields);
    }
}
