using CoreChoice.Application;
using CoreChoice.Data;
using CoreChoice.Platforms.Android;
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

        // Android draws its own focus underline under every Editor and Entry, tinted from the
        // platform theme's accent — a blue that belongs to no palette in this app and does not
        // follow a theme switch. The approved design has no underline under these fields at all;
        // the card border already delimits them. So the platform drawable is removed rather than
        // re-tinted, which also removes one more colour that would need keeping in sync.
#if ANDROID
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping(
            "CoreChoiceNoUnderline",
            (handler, _) => handler.PlatformView.Background = null);
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
            "CoreChoiceNoUnderline",
            (handler, _) => handler.PlatformView.Background = null);
#endif

        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<IThemeStore>(sp => sp.GetRequiredService<ThemeService>());

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
        builder.Services.AddTransient<IPersonaCatalog>(sp => sp.GetRequiredService<CoreChoiceApiClient>());

        // Voice dictation for the dilemma fields. The permission prompt happens inside
        // AndroidVoiceDictation.ListenAsync, at the moment the mic is tapped — never here.
        builder.Services.AddTransient<IVoiceDictation, AndroidVoiceDictation>();

        // The database path is a MAUI concern (FileSystem.AppDataDirectory), so it is resolved
        // here, in the composition root, and nowhere else. LocalDbContext and
        // SqliteProfileRepository stay free of MAUI types so CoreChoice.App.Tests, a plain
        // net10.0 project, can link them by source.
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "corechoice.db");
        builder.Services.AddDbContextFactory<LocalDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddSingleton<IProfileRepository, SqliteProfileRepository>();
        builder.Services.AddSingleton<IDecisionHistory, SqliteDecisionHistory>();

        // The personality-test flow: intro, the 50 items, and the free result.
        builder.Services.AddTransient<TestIntroViewModel>();
        builder.Services.AddTransient<TestViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<TestIntroPage>();
        builder.Services.AddTransient<TestPage>();
        builder.Services.AddTransient<ProfilePage>();

        // Asking a question: the dilemma screen and the advisor-persona choice.
        builder.Services.AddTransient<DilemmaViewModel>();
        builder.Services.AddTransient<PersonaViewModel>();
        builder.Services.AddTransient<DilemmaPage>();
        builder.Services.AddTransient<PersonaPage>();

        // The payoff: the waiting screen and the answer itself.
        builder.Services.AddTransient<AnalysisViewModel>();
        builder.Services.AddTransient<AnalysisPage>();

        // The person's own record of what they have asked: the list tab and the full-answer
        // detail it opens onto.
        builder.Services.AddTransient<AnswersViewModel>();
        builder.Services.AddTransient<AnswersPage>();
        builder.Services.AddTransient<AnswerDetailViewModel>();
        builder.Services.AddTransient<AnswerDetailPage>();

        // The analyses/coins store. Real Google Play Billing on Android; buying itself is
        // disabled for now (see CoinsViewModel's own doc) since there is no backend endpoint yet
        // to redeem a purchase.
        //
        // Singleton, not transient: PlayBillingService owns a BillingClient connection that is
        // expensive to open and is meant to live for the app's whole session — a fresh AddTransient
        // instance on every navigation to the coins page opened a new connection that was then
        // never closed. One long-lived instance, closed by PlayBillingService.Dispose when the DI
        // container itself is disposed at app shutdown, matches how the Play Billing Library
        // itself expects to be used.
        builder.Services.AddSingleton<IBillingService, PlayBillingService>();
        builder.Services.AddTransient<CoinsViewModel>();
        builder.Services.AddTransient<CoinsPage>();

        // Appearance settings — the only screen that offers a palette or light/dark/system
        // choice. Reached from the dilemma screen, never from onboarding.
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        var app = builder.Build();

        // EF Core never creates a SQLite file's tables on its own — only migrating or calling
        // EnsureCreated does. Without this, the very first launch's very first query (from
        // TestIntroViewModel.LoadAsync, before anything else has run) would fail against a
        // schema-less database file with "no such table". A fresh install has zero rows, so this
        // check is effectively instant; the composition root is the one place doing it
        // synchronously is appropriate, since nothing can query the repository before it returns.
        using (var db = app.Services.GetRequiredService<IDbContextFactory<LocalDbContext>>().CreateDbContext())
            db.Database.EnsureCreated();

        return app;
    }
}
