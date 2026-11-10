using TaskManager.Users.Infrastructure.Security;

namespace TaskManager.Users.Infrastructure.Tests;

public sealed class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_Verify_succeeds()
    {
        var hash = _hasher.Hash("Secret123");
        _hasher.Verify(hash, "Secret123").ShouldBeTrue();
    }

    [Fact]
    public void Verify_wrong_password_fails()
    {
        var hash = _hasher.Hash("Secret123");
        _hasher.Verify(hash, "Wrong123").ShouldBeFalse();
    }

    [Fact]
    public void Verify_null_hash_returns_false()
    {
        _hasher.Verify(null, "Secret123").ShouldBeFalse();
    }

    [Fact]
    public void Hash_does_not_contain_plain_password()
    {
        _hasher.Hash("Secret123").ShouldNotContain("Secret123");
    }
}
