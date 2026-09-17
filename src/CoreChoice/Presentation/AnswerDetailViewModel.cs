using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;
using CoreChoice.Services;

namespace CoreChoice.Presentation;

/// <summary>
/// Backs the full-answer screen a row on the Answers tab opens onto. Renders the answer itself
/// through the shared <see cref="Controls.AnswerView"/> — the same control <see cref="AnalysisPage"/>
/// uses — so this file only needs to expose what that control does not already cover: which
/// advisor answered and how much the decision was weighed, both of which the live analysis screen
/// shows differently (its own header row) than a reopened past decision needs to.
///
/// No MAUI type appears anywhere in this file: <c>CoreChoice.App.Tests</c> links it in by source.
/// </summary>
public sealed partial class AnswerDetailViewModel(IDecisionHistory history) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDecision))]
    [NotifyPropertyChangedFor(nameof(Analysis))]
    [NotifyPropertyChangedFor(nameof(Verdict))]
    [NotifyPropertyChangedFor(nameof(IsVerdictStrong))]
    [NotifyPropertyChangedFor(nameof(PersonaDisplayName))]
    [NotifyPropertyChangedFor(nameof(WeightLabel))]
    [NotifyPropertyChangedFor(nameof(OptionA))]
    [NotifyPropertyChangedFor(nameof(OptionB))]
    [NotifyPropertyChangedFor(nameof(Context))]
    [NotifyPropertyChangedFor(nameof(HasContext))]
    [NotifyPropertyChangedFor(nameof(AskedAtDisplay))]
    private PastDecision? decision;

    /// <summary>True once <see cref="LoadAsync"/> has run and found nothing at that id — the
    /// entry was deleted from elsewhere, or the id was never valid. The page uses this to show a
    /// plain "this answer is gone" message instead of a blank screen.</summary>
    [ObservableProperty]
    private bool notFound;

    public bool HasDecision => Decision is not null;

    public DecisionAnalysis? Analysis => Decision?.Analysis;

    /// <summary>The four-word verdict, never the raw confidence number — see
    /// <see cref="VerdictLabel"/>'s own doc for why the number itself never reaches a screen.</summary>
    public string Verdict => Decision is null ? string.Empty : VerdictLabel.For(Decision.Analysis.Confidence);

    public bool IsVerdictStrong => Decision is not null && VerdictLabel.IsStrong(Decision.Analysis.Confidence);

    /// <summary>Resolved offline, the same table <see cref="DilemmaViewModel"/> falls back to —
    /// a past decision has no live catalog round trip to prefer over it.</summary>
    public string PersonaDisplayName => Decision is null ? string.Empty : PersonaDisplayNames.DisplayName(Decision.Persona.Value);

    public string WeightLabel => Decision is null ? string.Empty : DecisionWeightLabel.For(Decision.Weight.Value);

    public string OptionA => Decision?.Dilemma.OptionA ?? string.Empty;

    public string OptionB => Decision?.Dilemma.OptionB ?? string.Empty;

    public string Context => Decision?.Dilemma.Context ?? string.Empty;

    public bool HasContext => !string.IsNullOrEmpty(Context);

    public string AskedAtDisplay => Decision is null
        ? string.Empty
        : Decision.AskedAt.ToString("MMM d, yyyy 'at' h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    public async Task LoadAsync(long id, CancellationToken ct = default)
    {
        Decision = await history.FindAsync(id, ct);
        NotFound = Decision is null;
    }

    /// <summary>Removes this entry, at the person's request — their record is theirs to edit.
    /// Does nothing if nothing has been loaded.</summary>
    public Task DeleteAsync(CancellationToken ct = default) =>
        Decision is null ? Task.CompletedTask : history.DeleteAsync(Decision.Id, ct);
}
