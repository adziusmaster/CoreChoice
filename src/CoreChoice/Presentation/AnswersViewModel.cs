using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;

namespace CoreChoice.Presentation;

/// <summary>
/// Backs the Answers tab: the person's own record of every decision they have asked, newest
/// first. No MAUI type appears anywhere in this file, same discipline as every other view model in
/// this namespace — <c>CoreChoice.App.Tests</c> links this in by source.
///
/// <see cref="LoadAsync"/> is meant to be called every time the tab appears, not once: the list
/// changes from three different directions this view model has no other way to hear about — a
/// fresh analysis recorded by <see cref="AnalysisViewModel"/>, a deletion made from
/// <see cref="AnswerDetailPage"/>, or simply returning to this tab after either. A failed read is
/// swallowed rather than surfaced as an error screen — the empty state and a genuinely-empty
/// history look identical to the person, and a storage hiccup here is worth far less than the
/// screen staying usable.
/// </summary>
public sealed partial class AnswersViewModel(IDecisionHistory history) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDecisions))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private IReadOnlyList<PastDecisionRow> decisions = [];

    public bool HasDecisions => Decisions.Count > 0;

    /// <summary>Drives the empty-state message: someone who has asked nothing yet should see an
    /// explanation, not a blank page.</summary>
    public bool IsEmpty => !HasDecisions;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var all = await history.ListAsync(ct);
            Decisions = all.Select(PastDecisionRow.From).ToList();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // The list simply stays whatever it last was; a storage hiccup must not turn the
            // Answers tab into an error screen.
        }
    }
}
