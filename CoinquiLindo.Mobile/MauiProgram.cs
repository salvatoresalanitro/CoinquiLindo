using CoinquiLindo.Client;
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

            string apiBaseAddress = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5273"
            : "http://localhost:5273";

            // add http client
            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(apiBaseAddress),
                Timeout = TimeSpan.FromSeconds(15)
            });

            // add our services
            builder.Services.AddScoped<CoinquiLindoApiClient>();

            return builder.Build();
        }
    }
}
