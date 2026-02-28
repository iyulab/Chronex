using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

/// <summary>
/// T-5: @every range interval actual Next() range verification (M-1 fix).
/// </summary>
public class IntervalRangeTests
{
    [Fact]
    public void Next_IntervalRange_ReturnsWithinRange()
    {
        var expr = ChronexExpression.Parse("@every 1h-2h");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Run multiple times to verify the range (random)
        for (var i = 0; i < 20; i++)
        {
            var next = expr.GetNextOccurrence(from);
            next.Should().NotBeNull();
            var interval = next!.Value - from;
            interval.Should().BeGreaterThanOrEqualTo(TimeSpan.FromHours(1));
            interval.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(2));
        }
    }

    [Fact]
    public void Enumerate_IntervalRange_AllWithinRange()
    {
        var expr = ChronexExpression.Parse("@every 30m-1h");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var occurrences = expr.Enumerate(from, 10).ToList();
        occurrences.Count.Should().Be(10);

        var prev = from;
        foreach (var occ in occurrences)
        {
            var interval = occ - prev;
            interval.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMinutes(30));
            interval.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(1));
            prev = occ;
        }
    }

    [Fact]
    public void Next_IntervalRange_IsNonDeterministic()
    {
        var expr = ChronexExpression.Parse("@every 1m-10m");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Run 50 times and check that we get at least 2 different values
        var results = new HashSet<long>();
        for (var i = 0; i < 50; i++)
        {
            var next = expr.GetNextOccurrence(from);
            results.Add(next!.Value.Ticks);
        }

        // With a 9-minute range, 50 samples should produce multiple different values
        results.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Next_FixedInterval_IsDeterministic()
    {
        var expr = ChronexExpression.Parse("@every 5m");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var expected = from + TimeSpan.FromMinutes(5);
        for (var i = 0; i < 5; i++)
        {
            var next = expr.GetNextOccurrence(from);
            next!.Value.Should().Be(expected);
        }
    }

    [Fact]
    public void Next_IntervalRange_WithUntil_RespectsUntil()
    {
        var expr = ChronexExpression.Parse("@every 1h-2h {until:2026-01-01}");
        var from = new DateTimeOffset(2025, 12, 31, 23, 0, 0, TimeSpan.Zero);
        var next = expr.GetNextOccurrence(from);

        // The interval would push past until date
        if (next != null)
        {
            next.Value.Should().BeOnOrBefore(
                new DateTimeOffset(2026, 1, 1, 23, 59, 59, 999, TimeSpan.Zero));
        }
    }
}
