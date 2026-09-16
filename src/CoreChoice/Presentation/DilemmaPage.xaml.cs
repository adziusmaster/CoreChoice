namespace CoreChoice.Presentation;

/// <summary>
/// "What are you weighing?" — the two options, optional context, and how much rides on it. The
/// persona is chosen on a separate screen (<see cref="PersonaPage"/>) reached from the footer;
/// this page never fetches the persona catalog itself, so it never depends on the network to
/// render at all — only <see cref="DilemmaViewModel.BuildRequestAsync"/>, called once a persona
/// is already in hand, touches storage, and nothing here calls the backend.
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

    /// <summary>Receives the persona chosen on <see cref="PersonaPage"/>, passed back through the
    /// Shell navigation parameters on its way to "..".</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SelectedPersona", out var value) && value is Services.PersonaSummary persona)
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

        // The analysis/result screen ("getting an answer") is a separate feature, out of this
        // task's scope and not yet wired into the shell. Building the request here exercises the
        // full path this screen owns; returning to the free result is the safest known-good
        // destination until that screen exists, mirroring ProfilePage.OnAskClicked's placeholder.
        _ = await _viewModel.BuildRequestAsync();
        await Shell.Current.GoToAsync("//TestIntroPage");
    }
}
