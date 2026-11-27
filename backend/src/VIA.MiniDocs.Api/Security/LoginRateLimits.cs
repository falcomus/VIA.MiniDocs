using System.Security.Cryptography;
using System.Text;

namespace VIA.MiniDocs.Api.Security;

internal sealed class LoginRateLimits(TimeProvider clock)
{
  private const int GlobalPermits = 240;
  private const int IpPermits = 30;
  private const int NamePermits = 5;
  private const int PartitionCapacity = 1024;
  private readonly long windowTicks = clock.TimestampFrequency * 60;
  private readonly Lock gate = new();
  private readonly Dictionary<string, int> ipCounts = new(StringComparer.Ordinal);
  private readonly Dictionary<string, int> nameCounts = new(StringComparer.Ordinal);
  private long window = -1;
  private int total;

  internal bool TryAcquire(string ip, string normalizedName, out int retryAfter)
  {
    // Keys are bounded in-memory digests and never appear in diagnostics.
    var nameKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName)));
    lock (gate)
    {
      var now = clock.GetTimestamp();
      var currentWindow = now / windowTicks;
      if (window != currentWindow)
      {
        window = currentWindow;
        total = 0;
        ipCounts.Clear();
        nameCounts.Clear();
      }
      retryAfter = (int)Math.Ceiling((windowTicks - now % windowTicks) / (double)clock.TimestampFrequency);
      if (total >= GlobalPermits ||
          !CanSpend(ipCounts, ip, IpPermits, out var ipCount) ||
          !CanSpend(nameCounts, nameKey, NamePermits, out var nameCount))
        return false;

      // Publish all three counters only after every budget has admitted the request.
      ipCounts[ip] = ipCount + 1;
      nameCounts[nameKey] = nameCount + 1;
      total++;
      return true;
    }
  }

  private static bool CanSpend(Dictionary<string, int> partitions, string key, int permits, out int count) =>
      partitions.TryGetValue(key, out count) ? count < permits : partitions.Count < PartitionCapacity;
}
