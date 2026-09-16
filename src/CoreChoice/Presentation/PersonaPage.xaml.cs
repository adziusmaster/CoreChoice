using CoreChoice.Application;

namespace CoreChoice.Presentation;

/// <summary>
/// "Who should answer this?" — one suggestion with its reason, and the other five personas one
/// tap away. Deliberately not a grid of six: see <see cref="PersonaViewModel"/>'s own doc comment
/// for why. Tapping either the suggested persona's button or one of the "other voices" rows hands
/// the chosen <see cref="PersonaSummary"/> straight back to <see cref="DilemmaPage"/> through the
/// Shell navigation parameters — there is no separate confirmation step, on purpose: a second tap
/// to confirm a choice that was just made by tapping is exactly the friction this screen exists to
/// remove.
/// </summary>
public partial class PersonaPage : ContentPage, IQueryAttributable
{
    private readonly PersonaViewModel _viewModel;
    private int _weight = 3;

    public PersonaPage(PersonaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// <summary>Receives the current weight from <see cref="DilemmaPage"/>'s navigation query
    /// string, so the suggestion reflects the stake exactly as it stands when this screen is
    /// reached — the rule cross-references it directly, so a stale weight here would suggest the
    /// wrong persona.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("weight", out var value) && int.TryParse(value?.ToString(), out var weight))
            _weight = weight;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(_weight);
    }

    private async void OnAskSuggestedClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Suggested is { } persona)
            await ReturnWithAsync(persona);
    }

    private async void OnOtherPersonaTapped(object? sender, EventArgs e)
    {
        if (sender is Element { BindingContext: PersonaSummary persona })
            await ReturnWithAsync(persona);
    }

    private static Task ReturnWithAsync(PersonaSummary persona) =>
        Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["SelectedPersona"] = persona });
}
