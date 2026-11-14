using System.Security.Cryptography;
using System.Text;

namespace ServiceDesk.Api.Data;

public sealed class FirstRunSetup : IDisposable
{
    // Keep the first-run code in memory for this process and clear it once setup commits.
    private string? code;

    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal bool Required => Volatile.Read(ref code) is not null;

    internal string Begin()
    {
        var generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        Volatile.Write(ref code, generated);
        return generated;
    }

    internal bool Accepts(string? submitted)
    {
        var current = Volatile.Read(ref code);
        var candidate = submitted?.Trim();
        // The setup code uses uppercase hex and accepts lowercase when entered by hand.
        return current is not null && candidate?.Length == current.Length &&
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(current),
                Encoding.UTF8.GetBytes(candidate.ToUpperInvariant()));
    }

    internal void Complete() => Volatile.Write(ref code, null);

    public void Dispose() => Gate.Dispose();
}
