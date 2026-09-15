namespace CoreChoice.Application;

/// <summary>The stable, anonymous device identifier. Created once, then read from secure storage.</summary>
public interface IDeviceIdentity
{
    Task<Guid> GetOrCreateAsync(CancellationToken ct = default);
}

/// <summary>Speech to text. Implemented per platform; unavailable implementations report it rather than throwing.</summary>
public interface IVoiceDictation
{
    bool IsAvailable { get; }

    /// <summary>Returns the recognised text, or null when the person cancelled or said nothing.</summary>
    Task<string?> ListenAsync(CancellationToken ct = default);
}
