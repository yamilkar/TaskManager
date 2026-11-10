using TaskManager.SharedKernel.Security;

namespace TaskManager.SharedKernel.Tests;

public class JwtOptionsTests
{
    [Fact]
    public void JwtOptions_rejects_key_shorter_than_32_bytes()
        => Should.Throw<InvalidOperationException>(() => new JwtOptions { SigningKey = new string('k', 31) }.EnsureValid());

    [Fact]
    public void JwtOptions_accepts_32_byte_key()
        => Should.NotThrow(() => new JwtOptions { SigningKey = new string('k', 32) }.EnsureValid());

    [Fact]
    public void JwtOptions_has_expected_defaults()
    {
        var o = new JwtOptions();
        o.Issuer.ShouldBe("taskmanager-users");
        o.Audience.ShouldBe("taskmanager-api");
        o.LifetimeMinutes.ShouldBe(60);
        JwtOptions.SectionName.ShouldBe("Jwt");
    }
}
