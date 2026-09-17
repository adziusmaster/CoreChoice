using CoreChoice.Domain;

namespace CoreChoice.Application;

/// <summary>
/// One past decision, as it was asked and as it was answered. Held only on the device: the server
/// deliberately never stores the dilemma text or the profile, and the app's published privacy
/// policy says so, so history that outlived the request would contradict it.
/// </summary>
public sealed record PastDecision(
    long Id,
    Dilemma Dilemma,
    PersonaId Persona,
    DecisionWeight Weight,
    DecisionAnalysis Analysis,
    DateTimeOffset AskedAt);

/// <summary>
/// The person's own record of what they have asked. Kept indefinitely and never pruned — a decision
/// from two years ago is often exactly the one worth re-reading, and the whole history costs a
/// couple of megabytes at a rate no individual will reach.
/// </summary>
public interface IDecisionHistory
{
    /// <summary>Most recent first.</summary>
    Task<IReadOnlyList<PastDecision>> ListAsync(CancellationToken ct = default);

    Task<PastDecision?> FindAsync(long id, CancellationToken ct = default);

    /// <summary>Records an answered decision. Returns the stored row's id.</summary>
    Task<long> RecordAsync(
        Dilemma dilemma,
        PersonaId persona,
        DecisionWeight weight,
        DecisionAnalysis analysis,
        CancellationToken ct = default);

    /// <summary>Removes one entry, at the person's request. Their record is theirs to edit.</summary>
    Task DeleteAsync(long id, CancellationToken ct = default);
}
