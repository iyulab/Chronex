using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class ToStringTests
{
    [Fact]
    public void ToString_SimpleCron()
    {
        var expr = ChronexExpression.Parse("*/5 * * * *");
        expr.ToString().Should().Be("*/5 * * * *");
    }

    [Fact]
    public void ToString_WithTimezone()
    {
        var expr = ChronexExpression.Parse("TZ=UTC 0 9 * * MON-FRI");
        var str = expr.ToString();
        str.Should().StartWith("TZ=UTC");
        str.Should().Contain("0 9 * * MON-FRI");
    }

    [Fact]
    public void ToString_WithOptions()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {jitter:30s, max:10}");
        var str = expr.ToString();
        str.Should().Contain("0 9 * * *");
        str.Should().Contain("jitter:30s");
        str.Should().Contain("max:10");
    }

    [Fact]
    public void ToString_Interval()
    {
        var expr = ChronexExpression.Parse("@every 30m");
        expr.ToString().Should().Be("@every 30m");
    }

    [Fact]
    public void ToString_IntervalRange()
    {
        var expr = ChronexExpression.Parse("@every 1h-2h");
        expr.ToString().Should().Be("@every 1h-2h");
    }

    [Fact]
    public void ToString_Alias()
    {
        var expr = ChronexExpression.Parse("@daily");
        expr.ToString().Should().Be("@daily");
    }

    [Fact]
    public void ToString_Once()
    {
        var expr = ChronexExpression.Parse("@once 2025-03-01T09:00:00+09:00");
        var str = expr.ToString();
        str.Should().StartWith("@once");
        // Should contain the ISO datetime
        str.Should().Contain("2025-03-01");
    }

    [Fact]
    public void ToString_FullExpression()
    {
        var expr = ChronexExpression.Parse("TZ=UTC 0 9 * * MON-FRI {jitter:30s}");
        var str = expr.ToString();
        str.Should().StartWith("TZ=UTC");
        str.Should().Contain("0 9 * * MON-FRI");
        str.Should().Contain("jitter:30s");
    }

    [Fact]
    public void ScheduleOptions_ToString_Empty()
    {
        var opts = new ScheduleOptions();
        opts.ToString().Should().BeEmpty();
    }

    [Fact]
    public void ScheduleOptions_ToString_AllOptions()
    {
        var opts = ScheduleOptions.Parse("jitter:5m, stagger:3m, window:10m, max:100, tag:a+b");
        var str = opts.ToString();
        str.Should().Contain("jitter:5m");
        str.Should().Contain("stagger:3m");
        str.Should().Contain("window:10m");
        str.Should().Contain("max:100");
        str.Should().Contain("tag:a+b");
    }
}
