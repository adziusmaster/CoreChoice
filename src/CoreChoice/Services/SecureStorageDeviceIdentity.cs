using CoreChoice.Application;

namespace CoreChoice.Services;

/// <summary>
/// Persists an anonymous device GUID in <see cref="SecureStorage"/>, generated once on first
/// install. Falls back to <see cref="Preferences"/> on platforms/emulators where secure storage is
/// unavailable, so the app still works without crashing. Cached in memory after the first read.
///
/// Uses MAUI Essentials directly, which is why it lives outside the linked-source split that lets
/// the rest of the app's services run under plain `dotnet test`: this one is verified on-device.
/// </summary>
internal sealed class SecureStorageDeviceIdentity : IDeviceIdentity
{
    private const string StorageKey = "corechoice_device_id";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Guid _cached;

    public async Task<Guid> GetOrCreateAsync(CancellationToken ct = default)
    {
        if (_cached != Guid.Empty)
            return _cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached != Guid.Empty)
                return _cached;

            var stored = await SafeGetAsync();
            if (Guid.TryParse(stored, out var existing))
                return _cached = existing;

            var created = Guid.NewGuid();
            await SafeSetAsync(created.ToString());
            return _cached = created;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<string?> SafeGetAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(StorageKey);
        }
        catch
        {
            var fallback = Preferences.Default.Get(StorageKey, string.Empty);
            return string.IsNullOrEmpty(fallback) ? null : fallback;
        }
    }

    private static async Task SafeSetAsync(string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(StorageKey, value);
        }
        catch
        {
            Preferences.Default.Set(StorageKey, value);
        }
    }
}
