using Microsoft.Extensions.Configuration;

namespace TaskManager.ApiCommon.Tests;

public sealed class ForwardedHeadersSetupTests
{
    private static IConfiguration Configuration(string key, string value) =>
        new ConfigurationBuilder().AddInMemoryCollection([new KeyValuePair<string, string?>(key, value)]).Build();

    [Fact]
    public void TryCreateOptions_malformedProxy_throwsNamingKeyAndValue()
    {
        var ex = Should.Throw<InvalidOperationException>(() =>
            ForwardedHeadersSetup.TryCreateOptions(Configuration("ForwardedHeaders:KnownProxies:0", "not-an-ip"), out _));

        ex.Message.ShouldContain("ForwardedHeaders:KnownProxies");
        ex.Message.ShouldContain("not-an-ip");
    }

    [Fact]
    public void TryCreateOptions_malformedNetwork_throwsNamingKeyAndValue()
    {
        var ex = Should.Throw<InvalidOperationException>(() =>
            ForwardedHeadersSetup.TryCreateOptions(Configuration("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/99"), out _));

        ex.Message.ShouldContain("ForwardedHeaders:KnownNetworks");
        ex.Message.ShouldContain("10.0.0.0/99");
    }
}
