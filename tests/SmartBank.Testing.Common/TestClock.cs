using SmartBank.BuildingBlocks.Application;

namespace SmartBank.Testing.Common;

public sealed class TestClock : IClock
{
    private DateTimeOffset _now;

    public TestClock(DateTimeOffset? start = null) => _now = start ?? DateTimeOffset.UtcNow;

    public DateTimeOffset UtcNow => _now;

    public void Advance(TimeSpan span) => _now = _now.Add(span);

    public void Set(DateTimeOffset now) => _now = now;
}
