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
}
