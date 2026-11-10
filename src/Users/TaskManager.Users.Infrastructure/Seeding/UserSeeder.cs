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
    /// <summary>Idempotent: an already-registered demo email makes <c>TryAddAsync</c> return EmailTaken, which is ignored.</summary>
    public async Task SeedAsync(CancellationToken ct)
    {
        var seed = options.Value;

        var email = Email.Create(seed.DemoEmail);
        if (email.IsFailure)
        {
            throw new InvalidOperationException($"{UserSeedOptions.SectionName}:DemoEmail is not a valid email address.");
        }

        var user = User.Create(seed.DemoUserId, seed.DemoFullName, email.Value, hasher.Hash(seed.DemoPassword), clock.UtcNow);
        if (user.IsFailure)
        {
            throw new InvalidOperationException($"{UserSeedOptions.SectionName}:DemoFullName is invalid.");
        }

        await repository.TryAddAsync(user.Value, ct);
    }
}
