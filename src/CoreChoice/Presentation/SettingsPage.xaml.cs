using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>
/// "Appearance" (artboard 9) — the only screen in the app where a palette or light/dark/system
/// choice can be made. Reached from a small settings affordance on <see cref="DilemmaPage"/>, the
/// screen a returning person actually lands on; it is never offered during onboarding, so someone
/// who came here because choice exhausts them is not asked a six-way appearance question before
/// they have used the app once.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private void OnDarkTapped(object? sender, EventArgs e) => _viewModel.Select(ThemeMode.Dark);

    private void OnLightTapped(object? sender, EventArgs e) => _viewModel.Select(ThemeMode.Light);

    private void OnSystemTapped(object? sender, EventArgs e) => _viewModel.Select(ThemeMode.System);

    private void OnConsideredTapped(object? sender, EventArgs e) => _viewModel.Select(Palette.Considered);

    private void OnComposedTapped(object? sender, EventArgs e) => _viewModel.Select(Palette.Composed);

    private void OnStillTapped(object? sender, EventArgs e) => _viewModel.Select(Palette.Still);
}
