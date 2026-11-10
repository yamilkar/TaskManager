using TaskManager.SharedKernel;

namespace TaskManager.Users.Domain;

public sealed class User
{
    public const int FullNameMaxLength = 100;

    private User(Guid id, string fullName, Email email, string passwordHash, DateTimeOffset createdAt)
    {
        Id = id;
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public string FullName { get; }
    public Email Email { get; }
    public string PasswordHash { get; }
    public DateTimeOffset CreatedAt { get; }

    public static Result<User> Create(Guid id, string? fullName, Email email, string passwordHash, DateTimeOffset createdAt)
    {
        var trimmed = fullName?.Trim();
        string? problem = string.IsNullOrEmpty(trimmed)
            ? "Full name is required."
            : trimmed.Length > FullNameMaxLength ? "Full name must be at most 100 characters." : null;

        if (problem is not null)
        {
            return Error.Validation(
                "validation_failed",
                "One or more validation errors occurred.",
                new Dictionary<string, string[]> { ["fullName"] = [problem] });
        }

        return new User(id, trimmed!, email, passwordHash, createdAt);
    }

    public static User Rehydrate(Guid id, string fullName, Email email, string passwordHash, DateTimeOffset createdAt)
        => new(id, fullName, email, passwordHash, createdAt);
}
