namespace CoreChoice.Presentation;

/// <summary>
/// "Your profile" — the free, permanent result. Rendering here never waits on, or depends on the
/// success of, any coin-ledger call: <see cref="ProfileViewModel.LoadAsync"/> already guarantees
/// that structurally, so this page only needs to display what the view model exposes, not add any
/// network-aware logic of its own.
/// </summary>
public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnAskClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"//{nameof(DilemmaPage)}");

    /// <summary>
    /// "Retake the test" — confirms first, since fifty answers is ten minutes of someone's life
    /// and a mis-tap must not destroy it. On confirm, clears the stored answers (the profile shown
    /// on this very screen is left in place — see <see cref="ProfileViewModel.RetakeAsync"/>) and
    /// sends the person straight to the questionnaire rather than back through the intro screen.
    /// </summary>
    private async void OnRetakeClicked(object? sender, EventArgs e)
    {
        var confirmed = await RetakeConfirmation.ShowAsync(
            "Retake the test?",
            "This clears your fifty answers — about ten minutes of work — so you can start over. Your current profile stays exactly as it is until you finish the new one.",
            "Keep this profile",
            "Retake");

        if (!confirmed)
            return;

        await _viewModel.RetakeAsync();
        await Shell.Current.GoToAsync(nameof(TestPage));
    }
}
