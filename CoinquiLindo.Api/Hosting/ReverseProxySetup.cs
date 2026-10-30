using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace CoinquiLindo.Api.Hosting
{
    public static class ReverseProxySetup
    {
        public const string SectionName = "ReverseProxy";

        // Behind a TLS-terminating proxy (Dev Tunnels, App Service, Container Apps) Kestrel sees plain http:
        // without X-Forwarded-Proto/Host, OAuth would send Google an http redirect_uri and the login would fail
        public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

                // Loopback is trusted by default (Dev Tunnels host agent runs locally); add the platform proxy IPs in production
                foreach (var proxy in section.GetSection("KnownProxies").Get<string[]>() ?? [])
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }

                // Restricts which X-Forwarded-Host values are accepted (e.g. "*.devtunnels.ms", "api.example.com")
                foreach (var host in section.GetSection("AllowedHosts").Get<string[]>() ?? [])
                {
                    options.AllowedHosts.Add(host);
                }
            });

            return services;
        }
    }
}
