using CoinquiLindo.Client;
using CoinquiLindo.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace CoinquiLindo.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            // Session: tokens in SecureStorage, Bearer attached (and refreshed) by a single shared handler
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<SecureTokenStore>();
            builder.Services.AddSingleton<ITokenStore>(sp => sp.GetRequiredService<SecureTokenStore>());
            builder.Services.AddSingleton(sp => new AuthTokenHandler(sp.GetRequiredService<ITokenStore>(), sp.GetRequiredService<TimeProvider>())
            {
                InnerHandler = new HttpClientHandler()
            });

            // add http client (the shared handler must outlive each scoped HttpClient)
            builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<AuthTokenHandler>(), disposeHandler: false)
            {
                BaseAddress = ApiSettings.BaseAddress,
                Timeout = TimeSpan.FromSeconds(15)
            });

            // add our services
            builder.Services.AddScoped<CoinquiLindoApiClient>();
            builder.Services.AddScoped<AuthClient>();

            return builder.Build();
        }
    }
}
