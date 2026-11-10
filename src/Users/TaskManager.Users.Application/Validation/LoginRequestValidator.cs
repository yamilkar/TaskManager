using TaskManager.SharedKernel;
using TaskManager.Users.Application.Contracts;

namespace TaskManager.Users.Application.Validation;

public static class LoginRequestValidator
{
    public static Error? Validate(LoginRequest r)
    {
        var fields = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(r.Email))
        {
            fields["email"] = ["Email is required."];
        }

        if (string.IsNullOrEmpty(r.Password))
        {
            fields["password"] = ["Password is required."];
        }

        return fields.Count == 0
            ? null
            : Error.Validation("validation_failed", "One or more validation errors occurred.", fields);
    }
}
