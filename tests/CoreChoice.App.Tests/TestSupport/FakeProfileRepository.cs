using CoreChoice.Application;
using CoreChoice.Domain;

namespace CoreChoice.App.Tests;

/// <summary>
/// An in-memory <see cref="IProfileRepository"/> for view model tests that care about the
/// view model's own logic, not SQLite — persistence itself is already covered by
/// ProfileRepositoryTests against the real SQLite-backed implementation.
/// </summary>
internal sealed class FakeProfileRepository : IProfileRepository
{
    private readonly Dictionary<int, int> _answers = [];

    public OceanProfile SavedProfile { get; private set; } = OceanProfile.None;

    public int SaveProfileCallCount { get; private set; }

    public Task<StoredAnswers> LoadAnswersAsync(CancellationToken ct = default) =>
        Task.FromResult(new StoredAnswers(new Dictionary<int, int>(_answers), DateTimeOffset.UtcNow));

    public Task SaveAnswerAsync(int itemNumber, int response, CancellationToken ct = default)
    {
        _answers[itemNumber] = response;
        return Task.CompletedTask;
    }

    public Task ClearAnswersAsync(CancellationToken ct = default)
    {
        _answers.Clear();
        return Task.CompletedTask;
    }

    public Task<OceanProfile> LoadProfileAsync(CancellationToken ct = default) =>
        Task.FromResult(SavedProfile);

    public Task SaveProfileAsync(OceanProfile profile, CancellationToken ct = default)
    {
        SavedProfile = profile;
        SaveProfileCallCount++;
        return Task.CompletedTask;
    }

    /// <summary>Seeds answers directly, bypassing <see cref="SaveAnswerAsync"/>, to set up a
    /// "resuming" state without exercising the method under test.</summary>
    public void SeedAnswers(IEnumerable<KeyValuePair<int, int>> answers)
    {
        foreach (var (number, response) in answers)
            _answers[number] = response;
    }
}

/// <summary>An <see cref="ICoinLedgerClient"/> that throws on every member, for proving the
/// free-result invariant: a profile must render even when the ledger is entirely unreachable.</summary>
internal sealed class AlwaysThrowingCoinLedgerClient : ICoinLedgerClient
{
    public Task<CoinBalance> GetBalanceAsync(CancellationToken ct = default) =>
        throw new HttpRequestException("no signal");

    public Task<CoinBalance> EnsureSeededAsync(CancellationToken ct = default) =>
        throw new HttpRequestException("no signal");

    public Task<GrantResult> ClaimProfileGrantAsync(CancellationToken ct = default) =>
        throw new HttpRequestException("no signal");
}
