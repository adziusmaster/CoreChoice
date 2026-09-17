using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>Which free-text field a dictation request should fill.</summary>
public enum DilemmaField
{
    OptionA,
    OptionB,
    Context,
}

/// <summary>
/// Backs "What are you weighing?" — the two options, optional context, and how much rides on the
/// choice. No MAUI type appears anywhere in this file: <c>CoreChoice.App.Tests</c> links it in by
/// source, and a converter or helper that touched <c>Microsoft.Maui.*</c> here would silently drop
/// out of test coverage entirely (see <c>PresentationMath.cs</c>'s doc comment for the story of
/// exactly that happening to every converter in this app).
///
/// Building the actual <see cref="Dilemma"/> is deferred to <see cref="BuildRequestAsync"/> rather
/// than attempted on every keystroke: <see cref="Dilemma.Create"/> throws on an over-length option,
/// and a screen that constructs its domain object on every character typed would crash mid-sentence
/// the moment a person went one letter past the limit. <see cref="CanSubmit"/> is the guard that
/// keeps that constructor call from ever running against invalid input.
/// </summary>
public sealed partial class DilemmaViewModel(
    IProfileRepository repository, IVoiceDictation dictation, IPersonaCatalog personaCatalog)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(OptionACounter))]
    [NotifyPropertyChangedFor(nameof(ShowOptionACounter))]
    [NotifyPropertyChangedFor(nameof(OptionAIsOverLimit))]
    private string optionA = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(OptionBCounter))]
    [NotifyPropertyChangedFor(nameof(ShowOptionBCounter))]
    [NotifyPropertyChangedFor(nameof(OptionBIsOverLimit))]
    private string optionB = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(ContextCounter))]
    [NotifyPropertyChangedFor(nameof(ShowContextCounter))]
    [NotifyPropertyChangedFor(nameof(ContextIsOverLimit))]
    private string context = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightLabel))]
    private int weight = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FooterDisplayName))]
    private PersonaSummary? persona;

    /// <summary>
    /// The suggested persona's display name, resolved offline from <see cref="PersonaDisplayNames"/>
    /// against <see cref="PersonaSuggestion.PersonaId"/> so it is correct from the moment this view
    /// model is constructed — before <see cref="InitializeAsync"/> has loaded the real profile or
    /// reached the catalog, and using the default weight and <see cref="OceanProfile.None"/> in the
    /// same way the fields below default. <see cref="InitializeAsync"/> refines this once the real
    /// profile is known and again once the catalog answers; a weight change refines it via
    /// <c>OnWeightChanged</c>. Never shown directly — see <see cref="FooterDisplayName"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FooterDisplayName))]
    private string suggestedDisplayName = PersonaDisplayNames.DisplayName(PersonaSuggestion.PersonaId(3, OceanProfile.None));

    /// <summary>The stored profile, cached after <see cref="InitializeAsync"/> loads it once, so a
    /// weight change can recompute the suggestion without hitting storage again.</summary>
    private OceanProfile _profile = OceanProfile.None;

    /// <summary>Display names from the catalog, once it has answered. Null until then, so
    /// <see cref="RefreshSuggestedDisplayName"/> knows to fall back to <see cref="PersonaDisplayNames"/>.</summary>
    private IReadOnlyDictionary<string, string>? _catalogDisplayNames;

    /// <summary>Whether the mic control should even be shown. Backed by the port so an
    /// unavailable or permission-refused recogniser hides the button rather than the screen
    /// offering a control that will silently do nothing.</summary>
    public bool IsDictationAvailable => dictation.IsAvailable;

    /// <summary>
    /// What the footer names as the answerer: the explicitly chosen persona once there is one,
    /// otherwise the locally-computed suggestion. The design calls for a pre-made, visible,
    /// changeable suggestion rather than an empty "choose who answers" prompt — a suggestion that
    /// has to be sought before it is seen is not a suggestion, it hands the decision straight back
    /// to someone who came here because choices exhaust them. An explicit choice wins here only
    /// until the weight changes again: <see cref="OnWeightChanged(int)"/> clears <see cref="Persona"/>
    /// on every slider move, because a changed weight is a changed question, and the suggestion for
    /// it should be shown rather than silently kept stale under an earlier explicit pick.
    /// </summary>
    public string FooterDisplayName => Persona?.DisplayName ?? SuggestedDisplayName;

    /// <summary>Short, plain-language stand-in for the numeric weight, shown next to the slider.
    /// See <see cref="DecisionWeightLabel"/> for why this is not <see cref="DecisionWeight.Description"/>.</summary>
    public string WeightLabel => DecisionWeightLabel.For(Weight);

    /// <summary>
    /// False until both options are non-empty and within <see cref="Dilemma.MaxOptionLength"/>,
    /// and the optional context (if any) is within <see cref="Dilemma.MaxContextLength"/>. This is
    /// the only thing standing between a person's typing and <see cref="Dilemma.Create"/>'s
    /// constructor-time throw: an option one character over the limit must disable the button, not
    /// crash the screen the moment it happens.
    /// </summary>
    // A counter that appears only near the limit, and never truncates. Silently cutting a paste
    // decides FOR the person which words they lose, always from the end — which is where the
    // qualifier that changes the meaning usually sits. Keeping every character and refusing to
    // submit lets them choose what goes.
    private const double CounterAppearsAt = 0.8;

    public string OptionACounter => $"{OptionA.Trim().Length} / {Dilemma.MaxOptionLength}";

    public string OptionBCounter => $"{OptionB.Trim().Length} / {Dilemma.MaxOptionLength}";

    public string ContextCounter => $"{Context.Trim().Length} / {Dilemma.MaxContextLength}";

    public bool ShowOptionACounter => NearLimit(OptionA, Dilemma.MaxOptionLength);

    public bool ShowOptionBCounter => NearLimit(OptionB, Dilemma.MaxOptionLength);

    public bool ShowContextCounter => NearLimit(Context, Dilemma.MaxContextLength);

    public bool OptionAIsOverLimit => OptionA.Trim().Length > Dilemma.MaxOptionLength;

    public bool OptionBIsOverLimit => OptionB.Trim().Length > Dilemma.MaxOptionLength;

    public bool ContextIsOverLimit => Context.Trim().Length > Dilemma.MaxContextLength;

    private static bool NearLimit(string value, int limit) =>
        value.Trim().Length >= limit * CounterAppearsAt;

    public bool CanSubmit => IsValidOption(OptionA) && IsValidOption(OptionB) && IsValidContext(Context);

    private static bool IsValidOption(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is > 0 and <= Dilemma.MaxOptionLength;
    }

    private static bool IsValidContext(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed.Length <= Dilemma.MaxContextLength;
    }

    /// <summary>
    /// Fills <paramref name="field"/> from the platform's speech recogniser, or does nothing at
    /// all when dictation is unavailable, cancelled, or heard nothing. Never throws: an
    /// unavailable or refused microphone must hide the button (via <see cref="IsDictationAvailable"/>)
    /// rather than break the screen, and this method is the other half of that guarantee — it is
    /// safe to call even if a caller ignores the availability flag.
    /// </summary>
    public async Task DictateAsync(DilemmaField field, CancellationToken ct = default)
    {
        if (!dictation.IsAvailable)
            return;

        var heard = await dictation.ListenAsync(ct);
        if (heard is null)
            return;

        switch (field)
        {
            case DilemmaField.OptionA:
                OptionA = heard;
                break;
            case DilemmaField.OptionB:
                OptionB = heard;
                break;
            case DilemmaField.Context:
                Context = heard;
                break;
        }
    }

    /// <summary>
    /// Whenever the slider moves: clears any explicitly chosen <see cref="Persona"/> and
    /// recomputes <see cref="SuggestedDisplayName"/>, so the footer always follows the weight that
    /// is now current. A weight change means a changed question, so an earlier explicit choice
    /// must not keep answering it — the suggestion for the new weight takes back over until the
    /// person explicitly picks again. This never touches <see cref="Weight"/> itself, only the
    /// reverse never happens either: choosing a persona on the picker screen must never move this
    /// slider back.
    /// </summary>
    partial void OnWeightChanged(int value)
    {
        Persona = null;
        RefreshSuggestedDisplayName();
    }

    /// <summary>
    /// Loads the stored profile and resolves the suggested persona's name against it, then makes a
    /// best-effort attempt to refresh that name from the catalog. Called once, from the page's
    /// <c>OnAppearing</c>. The first step never touches the network — local storage only — so the
    /// footer is already correct by the time this returns even if the second step fails; a catalog
    /// failure here is silent for the same reason <see cref="PersonaViewModel.LoadAsync"/>'s is:
    /// this screen must render with no signal, and the local table already named the suggestion.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _profile = await repository.LoadProfileAsync(ct);
        RefreshSuggestedDisplayName();

        try
        {
            var personas = await personaCatalog.GetPersonasAsync(ct);
            _catalogDisplayNames = personas.ToDictionary(p => p.Id, p => p.DisplayName);
            RefreshSuggestedDisplayName();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // No network, or the server is unreachable — SuggestedDisplayName already carries the
            // local table's name from the step above, so the footer still names someone.
        }
    }

    private void RefreshSuggestedDisplayName()
    {
        var suggestedId = PersonaSuggestion.PersonaId(Weight, _profile);
        SuggestedDisplayName = _catalogDisplayNames is not null
            && _catalogDisplayNames.TryGetValue(suggestedId, out var catalogName)
                ? catalogName
                : PersonaDisplayNames.DisplayName(suggestedId);
    }

    /// <summary>
    /// "Ask this again" — puts a past decision's two options, context and weight back into this
    /// screen so they can be reworded, re-weighed, or handed to a different advisor. Deliberately
    /// leaves <see cref="Persona"/> unset rather than restoring the one that answered before:
    /// setting <see cref="Weight"/> below already fires <see cref="OnWeightChanged(int)"/>, which clears
    /// any persona and recomputes the suggestion for it, so "change advisor" falls out of the
    /// existing weight-change behaviour rather than needing a case of its own. Nothing here spends
    /// a coin — that only ever happens when the rebuilt request is actually submitted, exactly as
    /// a fresh question would.
    /// </summary>
    public void LoadFromPastDecision(PastDecision decision)
    {
        OptionA = decision.Dilemma.OptionA;
        OptionB = decision.Dilemma.OptionB;
        Context = decision.Dilemma.Context ?? string.Empty;
        Weight = Math.Clamp(decision.Weight.Value, DecisionWeight.Min, DecisionWeight.Max);
    }

    /// <summary>
    /// Builds the request the advisor will see, or <c>null</c> when the screen is not ready to
    /// submit — either the options are not both valid (<see cref="CanSubmit"/>) or no persona has
    /// been chosen yet. The profile is read fresh from storage rather than cached on this view
    /// model, so a request built right after finishing the test picks up the just-saved profile
    /// without this screen needing to know that happened.
    /// </summary>
    public async Task<DecisionRequest?> BuildRequestAsync(CancellationToken ct = default)
    {
        if (!CanSubmit || Persona is not { } persona)
            return null;

        if (!PersonaId.TryFrom(persona.Id, out var personaId))
            return null;

        var dilemma = Dilemma.Create(OptionA, OptionB, Context);
        var profile = await repository.LoadProfileAsync(ct);
        var clampedWeight = Math.Clamp(Weight, DecisionWeight.Min, DecisionWeight.Max);

        return new DecisionRequest(dilemma, profile, personaId, DecisionWeight.From(clampedWeight));
    }
}
