using TaskManager.SharedKernel;
using TaskManager.Users.Application.Ports;

namespace TaskManager.Users.Application.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId);

public sealed class GetCurrentUserHandler(IUserRepository users) : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(query.UserId, ct);
        return user is null ? UserErrors.NotFound : user.ToResponse();
    }
}
