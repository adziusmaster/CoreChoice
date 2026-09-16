using CoreChoice.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoreChoice;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();

        // Appearance must be applied here, not from MauiProgram: builder.Build() does not
        // construct App, so Application.Current is still null there and both Apply() and
        // ThemeService's RequestedThemeChanged subscription would silently no-op, leaving
        // every DynamicResource token unresolved. The base Application constructor has
        // already set Application.Current by the time this body runs.
        services.GetRequiredService<ThemeService>().Apply();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());
}
