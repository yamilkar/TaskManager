using Amazon.DynamoDBv2.Model;
using TaskManager.Infrastructure.DynamoDb;
using TaskManager.Users.Domain;

namespace TaskManager.Users.Infrastructure.Persistence;

public static class UserItemMapper
{
    public static string UserKey(Guid id) => $"USER#{id:D}";

    public static string EmailKey(Email email) => $"EMAIL#{email.Value}";

    public static Dictionary<string, AttributeValue> ToUserItem(User user) => new()
    {
        ["pk"] = UserKey(user.Id).ToAttr(),
        ["_id"] = user.Id.ToAttr(),
        ["full_name"] = user.FullName.ToAttr(),
        ["email"] = user.Email.Value.ToAttr(),
        ["passwordHash"] = user.PasswordHash.ToAttr(),
        ["createdAt"] = user.CreatedAt.ToAttr(),
    };

    public static Dictionary<string, AttributeValue> ToEmailGuardItem(User user) => new()
    {
        ["pk"] = EmailKey(user.Email).ToAttr(),
        ["userId"] = user.Id.ToAttr(),
    };

    public static User FromUserItem(IReadOnlyDictionary<string, AttributeValue> item)
    {
        var email = Email.Create(item.GetString("email"));
        if (email.IsFailure)
        {
            throw new InvalidOperationException("Stored user has an invalid email.");
        }

        return User.Rehydrate(
            item.GetGuid("_id"),
            item.GetString("full_name"),
            email.Value,
            item.GetString("passwordHash"),
            item.GetDateTimeOffset("createdAt"));
    }
}
