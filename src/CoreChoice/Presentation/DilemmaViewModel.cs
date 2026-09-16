using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Services;

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
public sealed partial class DilemmaViewModel(IProfileRepository repository, IVoiceDictation dictation)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private string optionA = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private string optionB = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private string context = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightLabel))]
    private int weight = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPersona))]
    [NotifyPropertyChangedFor(nameof(NoPersona))]
    private PersonaSummary? persona;

    /// <summary>Whether the mic control should even be shown. Backed by the port so an
    /// unavailable or permission-refused recogniser hides the button rather than the screen
    /// offering a control that will silently do nothing.</summary>
    public bool IsDictationAvailable => dictation.IsAvailable;

    /// <summary>True once a persona has been chosen on the picker screen.</summary>
    public bool HasPersona => Persona is not null;

    /// <summary>The inverse of <see cref="HasPersona"/>, exposed separately rather than negated
    /// in XAML with a converter — one fewer MAUI-typed file to keep in sync.</summary>
    public bool NoPersona => Persona is null;

    /// <summary>Short, plain-language stand-in for the numeric weight, shown next to the slider.
    /// Deliberately not <see cref="DecisionWeight.Description"/> — that full sentence is written
    /// for the advisor prompt, not a five-word label next to a slider.</summary>
    public string WeightLabel => Weight switch
    {
        <= 1 => "Barely anything",
        2 => "Not much",
        3 => "A fair amount",
        4 => "Quite a lot",
        _ => "A great deal",
    };

    /// <summary>
    /// False until both options are non-empty and within <see cref="Dilemma.MaxOptionLength"/>,
    /// and the optional context (if any) is within <see cref="Dilemma.MaxContextLength"/>. This is
    /// the only thing standing between a person's typing and <see cref="Dilemma.Create"/>'s
    /// constructor-time throw: an option one character over the limit must disable the button, not
    /// crash the screen the moment it happens.
    /// </summary>
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
