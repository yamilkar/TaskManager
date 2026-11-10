namespace TaskManager.Users.Domain.Tests;

public class UserTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Email AnEmail = Email.Create("ana@x.com").Value;

    [Fact]
    public void Create_trims_full_name()
    {
        var user = User.Create(Id, "  Ana  ", AnEmail, "hash", Now).Value;
        user.FullName.ShouldBe("Ana");
        user.Id.ShouldBe(Id);
        user.Email.ShouldBe(AnEmail);
        user.PasswordHash.ShouldBe("hash");
        user.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_full_name(string? name)
    {
        var result = User.Create(Id, name, AnEmail, "hash", Now);
        result.IsFailure.ShouldBeTrue();
        result.Error!.FieldErrors!["fullName"].ShouldContain("Full name is required.");
    }

    [Fact]
    public void Create_rejects_101_char_name()
    {
        var result = User.Create(Id, new string('a', 101), AnEmail, "hash", Now);
        result.Error!.FieldErrors!["fullName"].ShouldContain("Full name must be at most 100 characters.");
    }

    [Fact]
    public void Create_accepts_100_char_name()
        => User.Create(Id, new string('a', 100), AnEmail, "hash", Now).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Rehydrate_keeps_values()
    {
        var user = User.Rehydrate(Id, "Ana", AnEmail, "hash", Now);
        user.FullName.ShouldBe("Ana");
        user.Id.ShouldBe(Id);
    }
}
