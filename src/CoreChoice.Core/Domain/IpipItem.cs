namespace CoreChoice.Domain;

/// <summary>
/// One questionnaire item. <paramref name="Number"/> is the item's position in the standard IPIP
/// ordering and is the key answers are stored under on the device — renumbering silently re-keys
/// every saved profile, so the numbers are part of the contract, not presentation.
/// </summary>
public sealed record IpipItem(int Number, string Text, Trait Trait, bool IsReverseKeyed);
