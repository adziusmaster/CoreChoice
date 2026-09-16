using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>
/// The deterministic, local persona suggestion rule. Pulled out of <see cref="PersonaViewModel"/>
/// into its own static class so it can be swept exhaustively (weight × profile presence × the
/// neuroticism threshold) without needing an <see cref="IPersonaCatalog"/> fake for every case —
/// this class never touches the network, only the weight and the stored profile.
///
/// No MAUI type appears here, same as the rest of this file: <c>CoreChoice.App.Tests</c> links
/// <c>PersonaViewModel.cs</c> in by source.
/// </summary>
public static class PersonaSuggestion
{
    /// <summary>Neuroticism at or above this score is "high" for the purposes of the override.</summary>
    public const int NeuroticismHighThreshold = 67;

    /// <summary>
    /// weight 5 → the-long-view; 4 → pure-logic; 3 → the-pragmatist; 1-2 → gut-check. If the
    /// person has taken the test and scores high on neuroticism (≥ <see cref="NeuroticismHighThreshold"/>),
    /// warm-support is preferred instead, but only at weights 1-3 — a major, life-shaping decision
    /// (weight 4-5) still gets the analytical persona regardless of mood. devils-advocate is a real,
    /// selectable persona but is never the suggestion.
    /// </summary>
    public static string PersonaId(int weight, OceanProfile profile)
    {
        var clamped = Math.Clamp(weight, DecisionWeight.Min, DecisionWeight.Max);

        if (clamped <= 3 && profile.IsPresent && profile.Neuroticism.Value >= NeuroticismHighThreshold)
            return "warm-support";

        return clamped switch
        {
            5 => "the-long-view",
            4 => "pure-logic",
            3 => "the-pragmatist",
            _ => "gut-check",
        };
    }

    /// <summary>The one sentence explaining the suggestion above — shown so the recommendation
    /// can be accepted rather than re-decided.</summary>
    public static string Reason(int weight, OceanProfile profile)
    {
        var clamped = Math.Clamp(weight, DecisionWeight.Min, DecisionWeight.Max);

        if (clamped <= 3 && profile.IsPresent && profile.Neuroticism.Value >= NeuroticismHighThreshold)
            return "this tends to sit heavy on you, so a warmer voice fits before anything more clinical.";

        return clamped switch
        {
            5 => "you weighted this five out of five, and you are asking about years.",
            4 => "you weighted this four out of five — this calls for a clear-eyed look at the trade-offs.",
            3 => "you weighted this three out of five — a practical, grounded read fits.",
            _ => "you weighted this a small thing — a quick, decisive take fits.",
        };
    }
}

/// <summary>
/// Backs "Who should answer this?". Surfaces exactly one suggestion with its reason, never a grid
/// of six: this product exists to end choice paralysis, and six equally-weighted options in front
/// of someone who came here because of too many options is the app undermining its own premise.
/// The other five personas remain one tap away in <see cref="Others"/>.
/// </summary>
public sealed partial class PersonaViewModel(IPersonaCatalog personaCatalog, IProfileRepository repository)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestion))]
    private PersonaSummary? suggested;

    [ObservableProperty]
    private string suggestionReason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOthers))]
    private IReadOnlyList<PersonaSummary> others = [];

    public bool HasSuggestion => Suggested is not null;

    public bool HasOthers => Others.Count > 0;

    /// <summary>
    /// Computes the suggestion locally from <paramref name="weight"/> and the stored profile (no
    /// server call for that part), then fetches the catalog to resolve display names and split
    /// off the suggested persona from the rest. A catalog failure — no signal, a dead server —
    /// leaves <see cref="Suggested"/> null and <see cref="Others"/> empty rather than throwing: the
    /// suggestion rule itself never depends on the network, but its emphasis on the app's premise
    /// is worthless without a name and description to show for it.
    /// </summary>
    public async Task LoadAsync(int weight, CancellationToken ct = default)
    {
        var profile = await repository.LoadProfileAsync(ct);
        var suggestedId = PersonaSuggestion.PersonaId(weight, profile);
        SuggestionReason = PersonaSuggestion.Reason(weight, profile);

        IReadOnlyList<PersonaSummary> personas;
        try
        {
            personas = await personaCatalog.GetPersonasAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            Suggested = null;
            Others = [];
            return;
        }

        Suggested = personas.FirstOrDefault(p => p.Id == suggestedId);
        Others = personas.Where(p => p.Id != suggestedId).ToList();
    }
}
