using System.Text;

namespace TaskManager.SharedKernel.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    private const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "taskmanager-users";

    public string Audience { get; set; } = "taskmanager-api";

    public string SigningKey { get; set; } = "";

    public int LifetimeMinutes { get; set; } = 60;

    public void EnsureValid()
    {
        if (Encoding.UTF8.GetByteCount(SigningKey ?? "") < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"{SectionName}:SigningKey must be at least {MinimumKeyBytes} bytes.");
        }
    }
}
