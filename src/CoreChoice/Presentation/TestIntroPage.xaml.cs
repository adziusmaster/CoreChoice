namespace CoreChoice.Presentation;

/// <summary>
/// The screen shown before the test starts ("Before the test" in the design). Tells a
/// first-timer apart from someone resuming a partly answered test or retaking a finished one, and
/// points the primary button at the right next screen for each: <see cref="TestPage"/> for a
/// fresh start or a resume, <see cref="ProfilePage"/> straight away for a finished test — there is
/// no reason to walk someone back through fifty already-answered items.
/// </summary>
public partial class TestIntroPage : ContentPage
{
    private readonly TestIntroViewModel _viewModel;

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
            { AnsweredCount: var a, TotalCount: var t } when a >= t && _viewModel.HasExistingProfile => "View your profile",
            { IsResuming: true } => $"Resume · {_viewModel.AnsweredCount} of {_viewModel.TotalCount}",
            _ => "Begin",
        };
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        var goToProfile = _viewModel.AnsweredCount >= _viewModel.TotalCount && _viewModel.HasExistingProfile;

        await Shell.Current.GoToAsync(goToProfile ? nameof(ProfilePage) : nameof(TestPage));
    }
}
