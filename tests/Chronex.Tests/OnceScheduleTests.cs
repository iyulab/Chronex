using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class OnceScheduleTests
{
    [Fact]
    public void Parse_AbsoluteDatetime()
    {
        var os = OnceSchedule.Parse("2025-03-01T09:00:00+09:00");
        os.WasRelative.Should().BeFalse();
        os.RelativeDuration.Should().BeNull();
        os.FireAt.Should().Be(new DateTimeOffset(2025, 3, 1, 9, 0, 0, TimeSpan.FromHours(9)));
    }

    [Fact]
    public void Parse_AbsoluteUtc()
    {
        var os = OnceSchedule.Parse("2025-12-31T23:59:59Z");
        os.FireAt.Offset.Should().Be(TimeSpan.Zero);
        os.FireAt.Year.Should().Be(2025);
    }

    [Fact]
    public void Parse_RelativeDuration()
    {
        var refTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var os = OnceSchedule.Parse("+20m", refTime);
        os.WasRelative.Should().BeTrue();
        os.RelativeDuration.Should().NotBeNull();
        os.RelativeDuration!.Value.Value.Should().Be(TimeSpan.FromMinutes(20));
        os.FireAt.Should().Be(refTime + TimeSpan.FromMinutes(20));
    }

    [Fact]
    public void Parse_RelativeCompound()
    {
        var refTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var os = OnceSchedule.Parse("+1h30m", refTime);
        os.FireAt.Should().Be(refTime + TimeSpan.FromMinutes(90));
    }

    [Fact]
    public void Parse_Empty_Fails()
    {
        OnceSchedule.TryParse("", out _, out var error).Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Parse_InvalidDatetime_Fails()
    {
        OnceSchedule.TryParse("not-a-date", out _, out var error).Should().BeFalse();
        error!.Should().Contain("invalid datetime");
    }

    [Fact]
    public void Parse_InvalidRelativeDuration_Fails()
    {
        OnceSchedule.TryParse("+xyz", out _, out var error).Should().BeFalse();
        error!.Should().Contain("invalid relative duration");
    }

    // Integration: via ChronexExpression

    [Fact]
    public void ChronexExpression_ParsesInterval()
    {
        var expr = ChronexExpression.Parse("@every 30m");
        expr.Kind.Should().Be(ScheduleKind.Interval);
        expr.IntervalSchedule.Should().NotBeNull();
        expr.IntervalSchedule!.Value.Interval.Value.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void ChronexExpression_ParsesIntervalRange()
    {
        var expr = ChronexExpression.Parse("@every 1h-2h");
        expr.IntervalSchedule.Should().NotBeNull();
        expr.IntervalSchedule!.Value.IsRange.Should().BeTrue();
    }

    [Fact]
    public void ChronexExpression_ParsesOnceAbsolute()
    {
        var expr = ChronexExpression.Parse("@once 2025-03-01T09:00:00+09:00");
        expr.Kind.Should().Be(ScheduleKind.Once);
        expr.OnceSchedule.Should().NotBeNull();
        expr.OnceSchedule!.Value.WasRelative.Should().BeFalse();
    }

    [Fact]
    public void ChronexExpression_ParsesOnceRelative()
    {
        var refTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expr = ChronexExpression.Parse("@once +20m", refTime);
        expr.OnceSchedule.Should().NotBeNull();
        expr.OnceSchedule!.Value.WasRelative.Should().BeTrue();
        expr.OnceSchedule.Value.FireAt.Should().Be(refTime + TimeSpan.FromMinutes(20));
    }

    [Fact]
    public void ChronexExpression_IntervalWithOptions()
    {
        var expr = ChronexExpression.Parse("@every 5m {max:10}");
        expr.Kind.Should().Be(ScheduleKind.Interval);
        expr.IntervalSchedule.Should().NotBeNull();
        expr.OptionsRaw.Should().Be("max:10");
    }

    [Fact]
    public void ChronexExpression_OnceWithTimezone()
    {
        var expr = ChronexExpression.Parse("TZ=UTC @once 2025-03-01T09:00:00Z");
        expr.Timezone.Should().Be("UTC");
        expr.OnceSchedule.Should().NotBeNull();
    }
}
