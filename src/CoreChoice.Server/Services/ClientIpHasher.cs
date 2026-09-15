using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CoreChoice.Server.Services;

internal interface IClientIpHasher
{
    /// <summary>Salted, non-reversible token for an origin. Null when the address is unknown.</summary>
    string? Hash(IPAddress? address);

    /// <summary>
    /// Salted, non-reversible token for a device, for the usage log. The raw device id is never
    /// stored there: on its own it is innocuous, but joined to a usage history it becomes a record
    /// of what one identifiable person has been agonising over.
    /// </summary>
    string HashDevice(Guid deviceId);
}

/// <summary>
/// Turns a client address into a stable token the cap can count without the database becoming a
/// record of who used the app from where. The salt must stay stable across deploys — otherwise the
/// cap resets with every release — and stay secret, or the stored hashes become reversible by
/// anyone willing to enumerate the address space, which is small enough to be trivial.
/// </summary>
internal sealed class ClientIpHasher(string salt) : IClientIpHasher
{
    public string? Hash(IPAddress? address)
    {
        if (address is null) return null;
        return Token($"ip|{address}");
    }

    public string HashDevice(Guid deviceId) => Token($"device|{deviceId:N}");

    private string Token(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}|{value}")));
}
