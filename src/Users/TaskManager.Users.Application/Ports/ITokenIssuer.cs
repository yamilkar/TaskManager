using TaskManager.Users.Domain;

namespace TaskManager.Users.Application.Ports;

public interface ITokenIssuer
{
    IssuedToken Issue(User user);
}
