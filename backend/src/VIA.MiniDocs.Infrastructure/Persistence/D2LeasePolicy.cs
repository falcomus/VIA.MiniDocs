using System.Globalization;
using VIA.MiniDocs.Infrastructure.Persistence.Migrations;

namespace VIA.MiniDocs.Infrastructure;

internal static class D2LeasePolicy
{
  // PostgreSQL's function is built from this same versioned numeric resource.
  internal static int MaximumSeconds { get; } = int.Parse(
      FinalQualityHardening.Read("LeaseMaximumSeconds.txt").Trim(), CultureInfo.InvariantCulture);

  internal static TimeSpan MaximumDuration { get; } = TimeSpan.FromSeconds(MaximumSeconds);
}
