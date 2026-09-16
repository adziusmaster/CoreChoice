using CoreChoice.Application;
using CoreChoice.Data;
using CoreChoice.Presentation;
using CoreChoice.Services;
using Microsoft.EntityFrameworkCore;
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
        builder.Services.AddTransient<IDecisionClient>(sp => sp.GetRequiredService<CoreChoiceApiClient>());
        builder.Services.AddTransient<ICoinLedgerClient>(sp => sp.GetRequiredService<CoreChoiceApiClient>());

        // The database path is a MAUI concern (FileSystem.AppDataDirectory), so it is resolved
        // here, in the composition root, and nowhere else. LocalDbContext and
        // SqliteProfileRepository stay free of MAUI types so CoreChoice.App.Tests, a plain
        // net10.0 project, can link them by source.
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "corechoice.db");
        builder.Services.AddDbContextFactory<LocalDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddSingleton<IProfileRepository, SqliteProfileRepository>();

        // The personality-test flow: intro, the 50 items, and the free result.
        builder.Services.AddTransient<TestIntroViewModel>();
        builder.Services.AddTransient<TestViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<TestIntroPage>();
        builder.Services.AddTransient<TestPage>();
        builder.Services.AddTransient<ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
