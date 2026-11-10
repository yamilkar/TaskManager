namespace TaskManager.Users.Application.Ports;

public enum AddUserResult
{
    Added,

    /// <summary>The email guard already exists.</summary>
    EmailTaken,

    /// <summary>Concurrent writes to the same email kept conflicting after the retries; the caller may retry.</summary>
    Busy,
}
