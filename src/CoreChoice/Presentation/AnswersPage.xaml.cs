namespace CoreChoice.Presentation;

/// <summary>
/// The Answers tab: every decision the person has asked, newest first, or the empty-state message
/// when they have asked nothing yet. Reloads on every <see cref="OnAppearing"/>, not once — the
/// list changes from directions this page has no other way to hear about (a fresh analysis
/// recorded elsewhere, a deletion made from <see cref="AnswerDetailPage"/>), and a read against
/// the local database is cheap enough that reloading every time is simpler than tracking whether
/// anything actually changed.
/// </summary>
public partial class AnswersPage : ContentPage
{
    private readonly AnswersViewModel _viewModel;

    public AnswersPage(AnswersViewModel viewModel)
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

    private async void OnRowTapped(object? sender, EventArgs e)
    {
        if (sender is Element { BindingContext: PastDecisionRow row })
            await Shell.Current.GoToAsync($"{nameof(AnswerDetailPage)}?id={row.Id}");
    }
}
