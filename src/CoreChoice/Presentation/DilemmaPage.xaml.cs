namespace CoreChoice.Presentation;

/// <summary>
/// "What are you weighing?" — the two options, optional context, and how much rides on it. The
/// persona is chosen on a separate screen (<see cref="PersonaPage"/>) reached from the footer, but
/// this page's own footer already names a suggested advisor before that screen is ever visited:
/// <see cref="DilemmaViewModel.SuggestedDisplayName"/> is correct offline from construction, and
/// <see cref="DilemmaViewModel.InitializeAsync"/> — called below, from <see cref="OnAppearing"/> —
/// only ever refines it further. A dead network leaves the footer exactly as good as it was, never
/// worse, so this page still renders correctly with no signal.
/// </summary>
public partial class DilemmaPage : ContentPage, IQueryAttributable
{
    private readonly DilemmaViewModel _viewModel;

    public DilemmaPage(DilemmaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    /// <summary>Receives the persona chosen on <see cref="PersonaPage"/>, passed back through the
    /// Shell navigation parameters on its way to "..".</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SelectedPersona", out var value) && value is Application.PersonaSummary persona)
            _viewModel.Persona = persona;
    }

    private async void OnSpeakOptionAClicked(object? sender, EventArgs e) =>
        await _viewModel.DictateAsync(DilemmaField.OptionA);

    private async void OnSpeakOptionBClicked(object? sender, EventArgs e) =>
        await _viewModel.DictateAsync(DilemmaField.OptionB);

    private async void OnChoosePersonaTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync($"{nameof(PersonaPage)}?weight={_viewModel.Weight}");

    private async void OnThinkItThroughClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.CanSubmit)
            return;

        if (_viewModel.Persona is null)
        {
            await Shell.Current.GoToAsync($"{nameof(PersonaPage)}?weight={_viewModel.Weight}");
            return;
        }

        var request = await _viewModel.BuildRequestAsync();
        if (request is null)
            return;

        await Shell.Current.GoToAsync(nameof(AnalysisPage), new Dictionary<string, object>
        {
            ["Request"] = request,
            ["PersonaDisplayName"] = _viewModel.FooterDisplayName,
        });
    }
}
