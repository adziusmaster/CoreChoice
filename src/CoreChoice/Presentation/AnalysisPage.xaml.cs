using CoreChoice.Application;

namespace CoreChoice.Presentation;

/// <summary>
/// The payoff screen: artboard 6 ("While it thinks") while <see cref="AnalysisViewModel.IsWorking"/>
/// is true, artboard 7 ("The answer") once <see cref="AnalysisViewModel.HasAnalysis"/> is true, and
/// a third state — not covered by either artboard — while <see cref="AnalysisViewModel.HasError"/>
/// is true. The three sections are laid out as overlapping <c>Grid</c> children in
/// <c>AnalysisPage.xaml</c>, each shown or hidden by its own <c>IsVisible</c> binding, so exactly
/// one is ever visible at a time.
///
/// Receives the already-built <see cref="DecisionRequest"/> and the chosen persona's display name
/// through Shell navigation parameters, the same mechanism <see cref="PersonaPage"/> uses to hand
/// the chosen persona back to <see cref="DilemmaPage"/> — passing the request as an object rather
/// than flattening it into a query string keeps its nested value objects (the profile, the
/// dilemma) intact rather than round-tripping them through string parsing.
/// </summary>
public partial class AnalysisPage : ContentPage, IQueryAttributable
{
    private readonly AnalysisViewModel _viewModel;
    private DecisionRequest? _request;
    private bool _hasAsked;

    public AnalysisPage(AnalysisViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Request", out var value) && value is DecisionRequest request)
            _request = request;

        if (query.TryGetValue("PersonaDisplayName", out var name) && name is string displayName)
            _viewModel.PersonaDisplayName = displayName;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Guard against asking twice: OnAppearing fires again when the "Both sides" state is
        // merely toggled or the app is resumed from background, and re-sending an already-paid-for
        // request would spend a second coin for nothing.
        if (_hasAsked || _request is null)
            return;

        _hasAsked = true;
        await _viewModel.AskAsync(_request);
    }

    private async void OnRetryClicked(object? sender, EventArgs e) =>
        await _viewModel.RetryAsync();

    private void OnBothSidesClicked(object? sender, EventArgs e) =>
        _viewModel.ToggleBothSides();

    /// <summary>
    /// "That settles it" — the flow is complete. The analysis/history screen this would naturally
    /// return to does not exist yet (out of this task's scope, same placeholder situation
    /// <see cref="DilemmaPage.OnThinkItThroughClicked"/> was already in), so this returns to the
    /// safest known-good screen until one does.
    /// </summary>
    private async void OnSettledClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//TestIntroPage");

    /// <summary>"Get more coins" — the analyses/coins store (artboard 8). Buying itself is
    /// disabled there for now (see <see cref="CoinsViewModel"/>'s own doc), but the balance and
    /// real Play Store prices are worth seeing regardless.</summary>
    private async void OnGetCoinsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(CoinsPage));

    private async void OnChangeQuestionClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
