using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// Backs the "your profile" screen. Scores and persists the Big Five profile the first time this
/// runs after the test completes, then tries — but never depends on — claiming the completion
/// coin grant. The test result is free: rendering it is never gated on network, balance, or the
/// ledger being reachable at all, because a person who just spent ten minutes on this may be
/// holding a phone with no signal.
/// </summary>
public sealed partial class ProfileViewModel(IProfileRepository repository, ICoinLedgerClient coinLedger)
    : ObservableObject
{
    [ObservableProperty]
    private OceanProfile profile = OceanProfile.None;

    [ObservableProperty]
    private string summary = string.Empty;

    /// <summary>
    /// The one locally-generated sentence of commentary on the result card — which trait pulls
    /// hardest from the middle, which pulls hardest the other way, and what that combination tends
    /// to cost. Built by <see cref="ProfileNoteComposer"/> from the profile already on the phone,
    /// never fetched, so it renders with no signal at all.
    /// </summary>
    [ObservableProperty]
    private string note = string.Empty;

    /// <summary>
    /// The per-trait "how you decide" passages that a large study actually supports, built locally
    /// by <see cref="ProfileTraitSummaryComposer"/> from the score already on the phone — same
    /// no-signal-required guarantee as <see cref="Note"/> above.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="InterpretedPassages"/> rather than badged inline, because the screen
    /// renders the two lists under two headings and a reader has to be able to tell which tier they
    /// are in without reading closely. Which traits land in which list depends on the profile: a
    /// score can be established at one end of a scale and pure interpretation in the middle, so
    /// either list may legitimately come back empty and the page hides the heading when it does.
    /// </remarks>
    [ObservableProperty]
    private IReadOnlyList<TraitPassage> evidencePassages = [];

    /// <summary>
    /// The per-trait passages where nothing is established: one way to read the score, or a fact
    /// about people in general. Never mixed into <see cref="EvidencePassages"/>.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<TraitPassage> interpretedPassages = [];

    [ObservableProperty]
    private bool hasEvidencePassages;

    [ObservableProperty]
    private bool hasInterpretedPassages;

    [ObservableProperty]
    private int balance;

    [ObservableProperty]
    private string? grantMessage;

    /// <summary>
    /// Renders the profile, scoring and saving it the first time if none is stored yet, then
    /// claims the completion grant.
    /// </summary>
    /// <remarks>
    /// <para><b>Precondition:</b> all fifty items must already be answered. Navigate here only once
    /// <see cref="TestViewModel.AdvanceAsync"/> has returned <c>true</c>. Called earlier, this
    /// throws <see cref="IncompleteProfileException"/> and does not catch it — scoring a partial set
    /// would produce a profile that looks plausible and is quietly wrong, which is worse than a
    /// loud failure. The page must not reach this screen speculatively.</para>
    /// <para>Claiming the grant can fail in any way at all — no signal, a dead server, a rate limit,
    /// a proxy returning HTML — and none of it may block the result. The personality test and its
    /// full result are free, permanently; that promise is kept here structurally rather than
    /// remembered, so the profile is rendered before the ledger is ever touched.</para>
    /// </remarks>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var stored = await repository.LoadProfileAsync(ct);

        if (!stored.IsPresent)
        {
            var answers = await repository.LoadAnswersAsync(ct);

            // This screen should only ever be reached at fifty of fifty answers. If it is not,
            // IpipScoring throws IncompleteProfileException rather than scoring the partial set —
            // a profile that looks plausible and is quietly wrong is worse than a loud failure.
            stored = IpipScoring.Score(answers.Responses);
            await repository.SaveProfileAsync(stored, ct);
        }

        Profile = stored;
        Summary = BuildSummary(stored);
        Note = ProfileNoteComposer.Compose(stored);
        var passages = ProfileTraitSummaryComposer.ComposeAll(stored);
        EvidencePassages = passages.Where(p => p.Tier == PassageTier.Established).ToArray();
        InterpretedPassages = passages.Where(p => p.Tier == PassageTier.Interpretation).ToArray();
        HasEvidencePassages = EvidencePassages.Count > 0;
        HasInterpretedPassages = InterpretedPassages.Count > 0;

        try
        {
            var grant = await coinLedger.ClaimProfileGrantAsync(ct);
            Balance = grant.Balance;
            GrantMessage = grant.Granted ? "You earned coins for finishing your profile." : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // No signal, a dead server, a rate limit — none of it may block the free result
            // already rendered above. The balance simply stays whatever it last was.
            GrantMessage = null;
        }
    }

    /// <summary>
    /// Clears the fifty stored answers so the test can be retaken, deliberately leaving the scored
    /// profile on <see cref="Profile"/> untouched — <see cref="IProfileRepository.ClearAnswersAsync"/>
    /// is specified to remove only the answers, never the profile, so someone who abandons a retake
    /// before finishing the new fifty is not left with nothing. Only a full new test, scored the
    /// next time <see cref="LoadAsync"/> runs from a completed resume, replaces it.
    /// </summary>
    /// <remarks>
    /// The confirmation a mis-tap must survive (fifty answers is ten minutes of someone's life) is
    /// the page's job, the one MAUI-dependent part of the retake flow — this method is called only
    /// once that confirmation has already been given.
    /// </remarks>
    public Task RetakeAsync(CancellationToken ct = default) => repository.ClearAnswersAsync(ct);

    private static string BuildSummary(OceanProfile profile) => string.Join(" · ",
    [
        $"Openness {profile.Openness.Band}",
        $"Conscientiousness {profile.Conscientiousness.Band}",
        $"Extraversion {profile.Extraversion.Band}",
        $"Agreeableness {profile.Agreeableness.Band}",
        $"Neuroticism {profile.Neuroticism.Band}",
    ]);
}
