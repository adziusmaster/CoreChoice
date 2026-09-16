using CommunityToolkit.Mvvm.ComponentModel;
using CoreChoice.Application;
using CoreChoice.Domain;

namespace CoreChoice.Presentation;

/// <summary>
/// One IPIP item as the test page renders it. <see cref="Response"/> is non-null only once this
/// session has tapped an answer for an item currently on <see cref="TestViewModel.CurrentPage"/> —
/// an item the repository already holds an answer for from an earlier session never appears on a
/// page again at all.
/// </summary>
public sealed record TestItem(int Number, string Text, int? Response);

/// <summary>
/// Drives the fifty-item personality test, three items at a time. Answers persist to
/// <see cref="IProfileRepository"/> the moment they are made, not on page turn, so a person killed
/// by Android mid-test (or who simply gives up) loses nothing already tapped. No MAUI type is
/// referenced here: the page that hosts this view model owns navigation and layout, this view
/// model only reports progress and when the last item has been answered.
/// </summary>
public sealed partial class TestViewModel(IProfileRepository repository) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdvance))]
    private IReadOnlyList<TestItem> currentPage = [];

    [ObservableProperty]
    private int answeredCount;

    public int TotalCount => IpipItemBank.ItemCount;

    /// <summary>
    /// True once every item on the current page carries a response. The page enables its "Next"
    /// control from this rather than from a raw count, so a partially answered page can never be
    /// advanced past.
    /// </summary>
    public bool CanAdvance => CurrentPage.Count > 0 && CurrentPage.All(i => i.Response.HasValue);

    /// <summary>Loads (or resumes) the test: the first unanswered trio, in bank order.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var stored = await repository.LoadAnswersAsync(ct);
        AnsweredCount = stored.Responses.Count;
        CurrentPage = BuildPage(stored.Responses);
    }

    /// <summary>
    /// Persists one tap immediately and reflects it on the current page without reshuffling the
    /// other items on it, so answering the first of three does not make the second and third jump
    /// before the person has finished the page.
    /// </summary>
    public async Task AnswerAsync(int itemNumber, int response, CancellationToken ct = default)
    {
        await repository.SaveAnswerAsync(itemNumber, response, ct);

        var stored = await repository.LoadAnswersAsync(ct);
        AnsweredCount = stored.Responses.Count;

        CurrentPage = CurrentPage
            .Select(i => i.Number == itemNumber ? i with { Response = response } : i)
            .ToList();
    }

    /// <summary>
    /// Moves on to the next unanswered trio. Returns true once there is nothing left to show —
    /// the page's cue to move on to the profile screen — or false with a freshly loaded page
    /// otherwise.
    /// </summary>
    public async Task<bool> AdvanceAsync(CancellationToken ct = default)
    {
        var stored = await repository.LoadAnswersAsync(ct);
        AnsweredCount = stored.Responses.Count;

        if (AnsweredCount >= IpipItemBank.ItemCount)
        {
            CurrentPage = [];
            return true;
        }

        CurrentPage = BuildPage(stored.Responses);
        return false;
    }

    private static IReadOnlyList<TestItem> BuildPage(IReadOnlyDictionary<int, int> responses) =>
        IpipItemBank.Items
            .Where(i => !responses.ContainsKey(i.Number))
            .OrderBy(i => i.Number)
            .Take(3)
            .Select(i => new TestItem(i.Number, Phrase(i.Text), null))
            .ToList();

    /// <summary>Prefixes the bank's bare stem with "I", lower-casing its first letter so
    /// "Am the life of the party." reads as "I am the life of the party."</summary>
    private static string Phrase(string stem) => $"I {char.ToLowerInvariant(stem[0])}{stem[1..]}";
}
