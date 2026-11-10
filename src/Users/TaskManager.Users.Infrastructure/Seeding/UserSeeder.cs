using Microsoft.Extensions.Options;
using TaskManager.SharedKernel;
using TaskManager.Users.Application.Ports;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Infrastructure.Seeding;

public sealed class UserSeeder(
    IUserRepository repository,
    IPasswordHasher hasher,
    IClock clock,
    IOptions<UserSeedOptions> options)
{
    /// <summary>
    /// Idempotent: does nothing when the demo email is already registered. The demo user has a fixed id,
    /// so re-adding it would fail the user item put too, which the repository rethrows rather than
    /// reporting EmailTaken.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct)
    {
        var seed = options.Value;

        var email = Email.Create(seed.DemoEmail);
        if (email.IsFailure)
        {
            throw new InvalidOperationException($"{UserSeedOptions.SectionName}:DemoEmail is not a valid email address.");
        }

        if (await repository.GetByEmailAsync(email.Value, ct) is not null)
        {
            return;
        }

        var user = User.Create(seed.DemoUserId, seed.DemoFullName, email.Value, hasher.Hash(seed.DemoPassword), clock.UtcNow);
        if (user.IsFailure)
        {
            throw new InvalidOperationException($"{UserSeedOptions.SectionName}:DemoFullName is invalid.");
        }

        await repository.TryAddAsync(user.Value, ct);
    }
}
