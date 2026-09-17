namespace CoreChoice.Presentation;

/// <summary>
/// The full answer behind one row on the Answers tab: recommendation, verdict, reasoning, both
/// options' strengths and risks, the personality note if there is one, which advisor answered, and
/// the weight — everything <see cref="AnalysisPage"/> shows for a live answer, reached here for a
/// past one instead. The answer body itself comes from the shared <see cref="Controls.AnswerView"/>;
/// this page only adds the persona/weight header and the dilemma's own text, which a reopened past
/// decision needs and the live screen already shows differently.
/// </summary>
public partial class AnswerDetailPage : ContentPage, IQueryAttributable
{
    private readonly AnswerDetailViewModel _viewModel;
    private long _id;

    public AnswerDetailPage(AnswerDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && long.TryParse(value?.ToString(), out var id))
            _id = id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(_id);
    }

    private async void OnBackTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");

    /// <summary>
    /// "Ask this again" — hands the dilemma, context and weight back to the Ask tab (see
    /// <see cref="DilemmaViewModel.LoadFromPastDecision"/>), without ever going through this
    /// page's own history entry again: nothing here spends a coin, that only happens if the
    /// person actually resubmits from the Ask screen.
    /// </summary>
    private async void OnAskAgainClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Decision is not { } decision)
            return;

        await Shell.Current.GoToAsync($"//{nameof(DilemmaPage)}", new Dictionary<string, object>
        {
            ["ReaskDecision"] = decision,
        });
    }

    /// <summary>Confirms first — the record is theirs to edit, but a mis-tap must not destroy it
    /// unconfirmed — using the same themed dialog <see cref="ProfilePage"/>'s retake flow uses.</summary>
    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var confirmed = await DeleteConfirmation.ShowAsync(
            "Delete this answer?",
            "This removes it from your answers for good. The rest of your history is untouched.",
            "Keep it",
            "Delete");

        if (!confirmed)
            return;

        await _viewModel.DeleteAsync();
        await Shell.Current.GoToAsync("..");
    }
}
