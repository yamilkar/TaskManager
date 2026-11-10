using TaskManager.Users.Domain;

namespace TaskManager.Users.Domain.Tests;

public class EmailTests
{
    [Theory]
    [InlineData(" Ana@X.com ", "ana@x.com")]
    [InlineData("BOB@EXAMPLE.ORG", "bob@example.org")]
    public void Create_normalizes(string raw, string expected)
        => Email.Create(raw).Value.Value.ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("a b@c.com")]
    public void Create_rejects_invalid(string? raw)
    {
        var result = Email.Create(raw);
        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("users.email_invalid");
        result.Error.FieldErrors!["email"].ShouldContain("Enter a valid email address.");
    }

    [Fact]
    public void Create_accepts_254_chars()
        => Email.Create(new string('a', 245) + "@test.com").IsSuccess.ShouldBeTrue();

    [Fact]
    public void Create_rejects_255_chars()
        => Email.Create(new string('a', 246) + "@test.com").IsFailure.ShouldBeTrue();
}
