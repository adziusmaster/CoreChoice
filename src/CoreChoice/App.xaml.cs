using CoreChoice.Application;
using CoreChoice.Presentation;
using CoreChoice.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoreChoice;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        // Appearance must be applied here, not from MauiProgram: builder.Build() does not
        // construct App, so Application.Current is still null there and both Apply() and
        // ThemeService's RequestedThemeChanged subscription would silently no-op, leaving
        // every DynamicResource token unresolved. The base Application constructor has
        // already set Application.Current by the time this body runs.
        services.GetRequiredService<ThemeService>().Apply();
    }

    // The five tab-root pages are resolved from the container (rather than left for AppShell.xaml's
    // own ShellContent/DataTemplate to construct) so each is guaranteed to receive its view model
    // by constructor injection.
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell(
            _services.GetRequiredService<TestIntroPage>(),
            _services.GetRequiredService<DilemmaPage>(),
            _services.GetRequiredService<AnswersPage>(),
            _services.GetRequiredService<CoinsPage>(),
            _services.GetRequiredService<SettingsPage>(),
            _services.GetRequiredService<ICoinLedgerClient>()));
}
