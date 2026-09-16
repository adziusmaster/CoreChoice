using CoreChoice.Services;
using Microsoft.Extensions.Logging;

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

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
