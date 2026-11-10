using TaskManager.Users.Domain;
using TaskManager.Users.Infrastructure.Persistence;

namespace TaskManager.Users.Infrastructure.Tests;

public sealed class UserItemMapperTests
{
    private static User NewUser()
    {
        var email = Email.Create("Ana@X.com").Value;
        return User.Rehydrate(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Ana", email, "hash", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Fact]
    public void ToUserItem_uses_spec_attribute_names()
    {
        var user = NewUser();
        var item = UserItemMapper.ToUserItem(user);

        item.Keys.OrderBy(k => k).ShouldBe(new[] { "_id", "createdAt", "email", "full_name", "passwordHash", "pk" });
        item["pk"].S.ShouldBe($"USER#{user.Id:D}");
    }

    [Fact]
    public void ToEmailGuardItem_keys_by_normalized_email()
    {
        var user = NewUser();
        var item = UserItemMapper.ToEmailGuardItem(user);

        item["pk"].S.ShouldBe("EMAIL#ana@x.com");
        item["userId"].S.ShouldBe(user.Id.ToString("D"));
    }

    [Fact]
    public void FromUserItem_round_trips_ToUserItem()
    {
        var user = NewUser();
        var back = UserItemMapper.FromUserItem(UserItemMapper.ToUserItem(user));

        back.Id.ShouldBe(user.Id);
        back.FullName.ShouldBe(user.FullName);
        back.Email.Value.ShouldBe(user.Email.Value);
        back.PasswordHash.ShouldBe(user.PasswordHash);
        back.CreatedAt.ShouldBe(user.CreatedAt);
    }
}
