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
/// <para>
/// Deployed topology (AWS): browser → Lambda Function URL, with no proxy in between, so leave this section unset;
/// the Lambda adapter sets the client IP from the request's <c>sourceIp</c>. <c>ForwardLimit = 1</c> supports exactly
/// one trusted proxy hop; a chain such as CloudFront → ALB is not a supported topology.
/// </para>
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
            if (!IPAddress.TryParse(proxy, out var address))
            {
                throw new InvalidOperationException($"{SectionName}:KnownProxies contains an invalid IP address: '{proxy}'.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var network in networks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out var range))
            {
                throw new InvalidOperationException($"{SectionName}:KnownNetworks contains an invalid CIDR range: '{network}'.");
            }

            options.KnownIPNetworks.Add(range);
        }

        return proxies.Length > 0 || networks.Length > 0;
    }

    public static IApplicationBuilder UseConfiguredForwardedHeaders(this IApplicationBuilder app, IConfiguration configuration) =>
        TryCreateOptions(configuration, out var options) ? app.UseForwardedHeaders(options) : app;
}
