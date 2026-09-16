using CoreChoice.Domain;

namespace CoreChoice.Application;

/// <summary>Answers saved so far, keyed by IPIP item number.</summary>
public sealed record StoredAnswers(IReadOnlyDictionary<int, int> Responses, DateTimeOffset UpdatedAt);

/// <summary>
/// Local, on-device storage for the personality test. Answers persist per item rather than on
/// submit, because a fifty-item questionnaire is abandoned mid-way by exactly the person this
/// product is for — low conscientiousness is, definitionally, difficulty finishing tasks.
///
/// Nothing here ever reaches the network. The profile leaves the device only inside a decision
/// request, and is not stored at the other end.
/// </summary>
public interface IProfileRepository
{
    Task<StoredAnswers> LoadAnswersAsync(CancellationToken ct = default);

    /// <summary>Persists one answer immediately. Called on every tap, not on page change.</summary>
    Task SaveAnswerAsync(int itemNumber, int response, CancellationToken ct = default);

    Task ClearAnswersAsync(CancellationToken ct = default);

    /// <summary>The scored profile, or <see cref="OceanProfile.None"/> when the test is unfinished.</summary>
    Task<OceanProfile> LoadProfileAsync(CancellationToken ct = default);

    Task SaveProfileAsync(OceanProfile profile, CancellationToken ct = default);
}
