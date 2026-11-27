using VIA.MiniDocs.Api.Security;
using Xunit;

namespace VIA.MiniDocs.Tests;

[Trait("Package", "D4-A")]
public sealed class LoginRateLimitTests
{
  [Fact]
  public void CapsIpNameAndGlobalWindowsAndClearsPartitions()
  {
    var clock = new TimestampClock();
    var limit = new LoginRateLimits(clock);
    for (var i = 0; i < 5; i++)
      Assert.True(limit.TryAcquire($"name-ip-{i}", "SAME-NAME", out _));
    Assert.False(limit.TryAcquire("name-ip-5", "SAME-NAME", out var retry));
    Assert.Equal(60, retry);
    for (var i = 0; i < 30; i++)
      Assert.True(limit.TryAcquire("same-ip", $"NAME-{i}", out _));
    Assert.False(limit.TryAcquire("same-ip", "ONE-MORE", out _));
    for (var i = 0; i < 205; i++)
      Assert.True(limit.TryAcquire($"global-ip-{i}", $"GLOBAL-{i}", out _));
    Assert.False(limit.TryAcquire("fresh-ip", "FRESH-NAME", out _));
    clock.Timestamp = 59;
    Assert.False(limit.TryAcquire("fresh-ip", "FRESH-NAME", out retry));
    Assert.Equal(1, retry);
    clock.Timestamp = 60;
    Assert.True(limit.TryAcquire("same-ip", "SAME-NAME", out _));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void ExhaustedPartitionCannotConsumeOtherBudgets(bool byName)
  {
    var limit = new LoginRateLimits(new TimestampClock());
    var accepted = byName ? 5 : 30;
    for (var i = 0; i < accepted; i++)
      Assert.True(limit.TryAcquire(byName ? $"ip-{i}" : "saturated-ip",
          byName ? "SATURATED-NAME" : $"user-{i}", out _));

    for (var i = 0; i < 1025; i++)
      Assert.False(limit.TryAcquire(byName ? "other-ip" : "saturated-ip",
          byName ? "SATURATED-NAME" : "OTHER-NAME", out _));

    // The rejected attempts consumed neither the global budget nor the other partition.
    Assert.True(limit.TryAcquire("other-ip", "OTHER-NAME", out _));
    for (var i = accepted + 1; i < 240; i++)
      Assert.True(limit.TryAcquire($"fresh-ip-{i}", $"fresh-name-{i}", out _));
    Assert.False(limit.TryAcquire("fresh-ip", "fresh-name", out _));
  }

  [Theory]
  [InlineData(true, false, 30)]
  [InlineData(false, true, 5)]
  [InlineData(false, false, 240)]
  public void ConcurrentAdmissionsStayWithinTheApplicableBudget(bool sameIp, bool sameName, int expected)
  {
    var limit = new LoginRateLimits(new TimestampClock());
    var admitted = 0;
    Parallel.For(0, 512, i =>
    {
      if (limit.TryAcquire(sameIp ? "same-ip" : $"ip-{i}", sameName ? "SAME-NAME" : $"NAME-{i}", out _))
        Interlocked.Increment(ref admitted);
    });
    Assert.Equal(expected, admitted);
  }

  private sealed class TimestampClock : TimeProvider
  {
    internal long Timestamp { get; set; }
    public override long TimestampFrequency => 1;
    public override long GetTimestamp() => Timestamp;
  }
}
