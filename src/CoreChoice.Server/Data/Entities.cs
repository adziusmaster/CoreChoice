namespace CoreChoice.Server.Data;

/// <summary>Coin balance for one anonymous device.</summary>
internal sealed class DeviceCoins
{
    public Guid DeviceId { get; set; }
    public int Balance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Records that a device was seeded with its first-contact coins, and from which origin. The origin
/// is a salted hash, never an address, so the per-origin cap works without the table becoming a log
/// of who used the app from where.
/// </summary>
internal sealed class DeviceSeed
{
    public Guid DeviceId { get; set; }
    public string? IpHash { get; set; }
    public DateTimeOffset SeededAt { get; set; }
}

/// <summary>
/// Records that a device already claimed the profile-completion grant. Existence of the row is the
/// whole anti-double-claim mechanism, which is why the endpoint is idempotent rather than erroring:
/// a retried call finds the row and returns the unchanged balance.
/// </summary>
internal sealed class ProfileGrant
{
    public Guid DeviceId { get; set; }
    public string? IpHash { get; set; }
    public int CoinsGranted { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}

/// <summary>
/// One analysis request, for cost accounting and abuse triage.
///
/// Stores a salted DeviceHash rather than the device id, and stores nothing of the dilemma or the
/// profile. Host-free by construction: there is no field here that could reconstruct what anyone
/// asked about.
/// </summary>
internal sealed class UsageLog
{
    public long Id { get; set; }
    public string DeviceHash { get; set; } = string.Empty;
    public string PersonaId { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public int Weight { get; set; }
    public bool Personalized { get; set; }
    public int PromptTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public bool Success { get; set; }
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// A promo code that grants a fixed number of analyses (coins) on redemption. One code may be
/// redeemed by many devices, but a revoked or expired code redeems for no one — see
/// <see cref="PromoRedemption"/> for the once-per-device enforcement.
/// </summary>
internal sealed class PromoCode
{
    /// <summary>Normalized (upper-case) code, e.g. "AB3KP".</summary>
    public string Code { get; set; } = string.Empty;
    public int Coins { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Optional expiry; null means the code never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>Set true to permanently disable the code regardless of expiry.</summary>
    public bool Revoked { get; set; }
}

/// <summary>
/// Records that a specific device redeemed a specific code. The composite primary key (Code,
/// DeviceId) is the entire once-per-device mechanism: a second insert for the same pair is a
/// database-level duplicate-key error, not a race a prior read could miss.
/// </summary>
internal sealed class PromoRedemption
{
    public string Code { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public int CoinsGranted { get; set; }
    public DateTimeOffset RedeemedAt { get; set; }
}

/// <summary>An advisor persona. Rows, not an enum, so adding one is an insert.</summary>
internal sealed class Persona
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// A versioned system prompt for one persona. Editing a prompt inserts a new version and flips the
/// active flag; UsageLog records which version answered, so "did that prompt change help?" is a
/// question the data can answer.
/// </summary>
internal sealed class PromptTemplate
{
    public long Id { get; set; }
    public string PersonaId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Template { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
