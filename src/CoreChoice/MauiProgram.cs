using CoreChoice.Application;
using CoreChoice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreChoice;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Lora-Regular.ttf", "Lora");
                fonts.AddFont("Lora-Medium.ttf", "LoraMedium");
                fonts.AddFont("Lora-SemiBold.ttf", "LoraSemiBold");
                fonts.AddFont("DMSans-Regular.ttf", "DMSans");
                fonts.AddFont("DMSans-Medium.ttf", "DMSansMedium");
                fonts.AddFont("DMSans-Bold.ttf", "DMSansBold");
            });

        builder.Services.AddSingleton<ThemeService>();

        // The backend client. A typed HttpClient with a 60-second timeout, not the default 30 —
        // the decision endpoint calls a language model, and 30 seconds is not always enough.
        builder.Services.AddSingleton<IDeviceIdentity, SecureStorageDeviceIdentity>();
        builder.Services.Configure<ApiOptions>(_ => { });
        builder.Services.AddHttpClient<CoreChoiceApiClient>((services, client) =>
        {
            var options = services.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        builder.Services.AddTransient<IDecisionAdvisor>(sp => sp.GetRequiredService<CoreChoiceApiClient>());
        builder.Services.AddTransient<ICoinLedgerClient>(sp => sp.GetRequiredService<CoreChoiceApiClient>());

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
