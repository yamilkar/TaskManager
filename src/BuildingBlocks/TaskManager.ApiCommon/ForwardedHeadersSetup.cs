using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace TaskManager.ApiCommon;

/// <summary>
/// Opt-in X-Forwarded-For support, so per-IP rate limits and logs see the real client behind a proxy
/// (CloudFront, an ALB). The header is trusted only when the TCP peer is a configured proxy:
/// <c>ForwardedHeaders:KnownProxies</c> (IP addresses) or <c>ForwardedHeaders:KnownNetworks</c> (CIDR ranges).
/// With neither configured the middleware is not added, so clients cannot spoof their IP.
/// </summary>
public static class ForwardedHeadersSetup
{
    public const string SectionName = "ForwardedHeaders";

    public static bool TryCreateOptions(IConfiguration configuration, out ForwardedHeadersOptions options)
    {
        var section = configuration.GetSection(SectionName);
        var proxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var networks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            // Only the address appended by the nearest trusted proxy is used.
            ForwardLimit = 1,
        };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var proxy in proxies)
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }

        return proxies.Length > 0 || networks.Length > 0;
    }

    public static IApplicationBuilder UseConfiguredForwardedHeaders(this IApplicationBuilder app, IConfiguration configuration) =>
        TryCreateOptions(configuration, out var options) ? app.UseForwardedHeaders(options) : app;
}
