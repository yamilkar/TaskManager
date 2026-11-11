namespace TaskManager.Users.Infrastructure.Seeding;

public sealed class UserSeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>The documented local demo password; refused outside Development.</summary>
    public const string PublicDemoPassword = "Demo123!";

    public bool Enabled { get; set; }
    public Guid DemoUserId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string DemoEmail { get; set; } = "demo@taskmanager.dev";
    public string DemoPassword { get; set; } = PublicDemoPassword;
    public string DemoFullName { get; set; } = "Demo User";
}
