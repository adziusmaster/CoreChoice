namespace CoreChoice.Presentation;

/// <summary>
/// The screen shown before the test starts ("Before the test" in the design), and the root
/// content of the bottom tab bar's Profile tab. Tells a first-timer apart from someone resuming a
/// partly answered test or retaking a finished one, and points the primary button at the right
/// next screen for each: <see cref="TestPage"/> for a fresh start or a resume, straight to
/// <see cref="ProfilePage"/> for a finished test — there is no reason to walk someone back through
/// fifty already-answered items.
///
/// As the Profile tab's root, this page must never let the tab land on <see cref="ProfilePage"/>
/// speculatively (see that page's view model's own doc on why): <see cref="OnAppearing"/> only
/// auto-navigates there once <see cref="TestIntroViewModel.ShouldShowProfile"/> says it is safe,
/// and only the first time this appearance happens per launch — <see cref="_autoNavigatedToProfile"/>
/// stops it from re-firing every time the person backs out of the profile screen to this one.
/// </summary>
public partial class TestIntroPage : ContentPage
{
    private readonly TestIntroViewModel _viewModel;
    private bool _autoNavigatedToProfile;

    public TestIntroPage(TestIntroViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.LoadAsync();

        StartButton.Text = _viewModel switch
        {
            { ShouldShowProfile: true } => "View your profile",
            { IsResuming: true } => $"Resume · {_viewModel.AnsweredCount} of {_viewModel.TotalCount}",
            _ => "Begin",
        };

        if (_viewModel.ShouldShowProfile && !_autoNavigatedToProfile)
        {
            _autoNavigatedToProfile = true;
            await Shell.Current.GoToAsync(nameof(ProfilePage));
        }
    }

    private async void OnStartClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(_viewModel.ShouldShowProfile ? nameof(ProfilePage) : nameof(TestPage));

    private async void OnAskAQuestionTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"//{nameof(DilemmaPage)}");
}
