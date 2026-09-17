using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// Backs the screen shown before the test starts. Its only job is telling a first-timer from
/// someone resuming a partly answered test or retaking a finished one — the copy and the "start"
/// control that react to that are the page's concern, not this view model's.
/// </summary>
public sealed partial class TestIntroViewModel(IProfileRepository repository) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResuming))]
    private int answeredCount;

    [ObservableProperty]
    private bool hasExistingProfile;

    public int TotalCount => IpipItemBank.ItemCount;

    /// <summary>True when some but not all items are answered — distinct from a fresh install
    /// (zero answered) and a finished test (<see cref="TotalCount"/> answered).</summary>
    public bool IsResuming => AnsweredCount > 0 && AnsweredCount < TotalCount;

    /// <summary>True once the test is finished and a profile has already been scored from it —
    /// the one condition under which it is safe to land on <c>ProfilePage</c> instead of this
    /// screen, since <c>ProfileViewModel.LoadAsync</c>'s precondition (all fifty items answered)
    /// is then guaranteed. Shared by the "Begin" / "View your profile" button and the Profile
    /// tab's own auto-navigation so the two never disagree about when it is safe to skip ahead.</summary>
    public bool ShouldShowProfile => AnsweredCount >= TotalCount && HasExistingProfile;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var stored = await repository.LoadAnswersAsync(ct);
        AnsweredCount = stored.Responses.Count;

        var profile = await repository.LoadProfileAsync(ct);
        HasExistingProfile = profile.IsPresent;
    }
}
