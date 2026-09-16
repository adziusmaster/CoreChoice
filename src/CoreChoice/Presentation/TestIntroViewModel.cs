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

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var stored = await repository.LoadAnswersAsync(ct);
        AnsweredCount = stored.Responses.Count;

        var profile = await repository.LoadProfileAsync(ct);
        HasExistingProfile = profile.IsPresent;
    }
}
