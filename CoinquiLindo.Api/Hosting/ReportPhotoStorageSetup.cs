using CoinquiLindo.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace CoinquiLindo.Api.Hosting
{
    public static class ReportPhotoStorageSetup
    {
        public static IServiceCollection AddReportPhotoStorage(this IServiceCollection services)
        {
            services.AddOptions<ReportPhotoStorageOptions>()
                .BindConfiguration(ReportPhotoStorageOptions.SectionName)
                .PostConfigure<IHostEnvironment>((options, environment) =>
                {
                    if (!string.IsNullOrWhiteSpace(options.RootPath))
                    {
                        options.RootPath = Path.GetFullPath(options.RootPath, environment.ContentRootPath);
                    }
                })
                .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath),
                    $"{ReportPhotoStorageOptions.SectionName}:RootPath must be configured.")
                .ValidateOnStart();

            return services;
        }
    }
}
