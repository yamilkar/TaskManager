using Microsoft.Extensions.DependencyInjection;
using TaskManager.SharedKernel;
using TaskManager.Users.Application.GetCurrentUser;
using TaskManager.Users.Application.Login;
using TaskManager.Users.Application.Register;

namespace TaskManager.Users.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<RegisterUserCommand, UserResponse>, RegisterUserHandler>();
        services.AddScoped<ICommandHandler<LoginCommand, AuthResponse>, LoginHandler>();
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, UserResponse>, GetCurrentUserHandler>();
        return services;
    }
}
