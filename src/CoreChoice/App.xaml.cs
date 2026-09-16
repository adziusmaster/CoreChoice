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

    // The root page is resolved from the container (rather than left for AppShell.xaml's own
    // ShellContent/DataTemplate to construct) so TestIntroPage is guaranteed to receive its
    // TestIntroViewModel by constructor injection.
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell(
            _services.GetRequiredService<TestIntroPage>(),
            _services.GetRequiredService<IProfileRepository>()));
}
