namespace TaskManager.Tasks.Infrastructure.Seeding;

public sealed class TaskSeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }
    public Guid DemoUserId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
}
