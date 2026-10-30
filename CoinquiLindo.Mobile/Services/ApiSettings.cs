using System.Reflection;

namespace CoinquiLindo.Mobile.Services
{
    public static class ApiSettings
    {
        // Injected at build time from the "ApiBaseUrl" MSBuild property (see CoinquiLindo.Mobile.csproj)
        public static Uri BaseAddress { get; } = new(
            typeof(ApiSettings).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(a => a.Key == "ApiBaseUrl")?.Value
            ?? throw new InvalidOperationException("ApiBaseUrl assembly metadata is missing: check CoinquiLindo.Mobile.csproj."));
    }
}
