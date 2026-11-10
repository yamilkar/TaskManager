using Microsoft.Extensions.Options;
using TaskManager.SharedKernel;
using TaskManager.Tasks.Application.Ports;
using TaskManager.Tasks.Domain;

namespace TaskManager.Tasks.Infrastructure.Seeding;

public sealed class TaskSeeder(ITaskRepository repository, IClock clock, IOptions<TaskSeedOptions> options)
{
    /// <summary>Idempotent: does nothing when the demo user already has any task.</summary>
    public async Task SeedAsync(CancellationToken ct)
    {
        var userId = options.Value.DemoUserId;

        if ((await repository.ListByUserAsync(userId, 1, null, ct)).Items.Count > 0)
        {
            return;
        }

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var seeds = new (string Title, string Description, TaskItemStatus Status, DateOnly? Due)[]
        {
            ("Explore the Task Manager", "Have a look around and try out the features.", TaskItemStatus.InProgress, today.AddDays(3)),
            ("Review sprint backlog", "Go through the upcoming work items.", TaskItemStatus.ToDo, today.AddDays(7)),
            ("Set up local environment", "Install the tooling and run the stack locally.", TaskItemStatus.Done, null),
            ("Write project notes", "Capture decisions and open questions.", TaskItemStatus.ToDo, null),
        };

        foreach (var seed in seeds)
        {
            var task = TaskItem.Create(Guid.NewGuid(), userId, seed.Title, seed.Description, seed.Status.ToString(), seed.Due, now);
            if (task.IsFailure)
            {
                throw new InvalidOperationException($"Demo task '{seed.Title}' is invalid.");
            }

            await repository.AddAsync(task.Value, ct);
        }
    }
}
