using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class EnumerateTests
{
    [Fact]
    public void Enumerate_FixedCount()
    {
        var expr = ChronexExpression.Parse("0 0 * * *");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from, 5).ToList();
        occurrences.Count.Should().Be(5);
        occurrences[0].DateTime.Should().Be(new DateTime(2026, 1, 2, 0, 0, 0));
        occurrences[4].DateTime.Should().Be(new DateTime(2026, 1, 6, 0, 0, 0));
    }

    [Fact]
    public void Enumerate_RespectsMax()
    {
        var expr = ChronexExpression.Parse("*/5 * * * * {max:3}");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from).ToList();
        occurrences.Count.Should().Be(3);
    }

    [Fact]
    public void Enumerate_RespectsUntil()
    {
        var expr = ChronexExpression.Parse("0 0 * * * {until:2026-01-05}");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from, 100).ToList();
        // Jan 2, 3, 4, 5 (until is end of Jan 5)
        occurrences.Count.Should().Be(4);
    }

    [Fact]
    public void Enumerate_Interval()
    {
        var expr = ChronexExpression.Parse("@every 1h");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from, 3).ToList();
        occurrences.Count.Should().Be(3);
        occurrences[0].Should().Be(from + TimeSpan.FromHours(1));
        occurrences[1].Should().Be(from + TimeSpan.FromHours(2));
        occurrences[2].Should().Be(from + TimeSpan.FromHours(3));
    }

    [Fact]
    public void Enumerate_Once_SingleOccurrence()
    {
        var expr = ChronexExpression.Parse("@once 2026-06-01T09:00:00Z");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from, 10).ToList();
        occurrences.Count.Should().Be(1);
    }

    [Fact]
    public void Enumerate_CountOverridesMax()
    {
        var expr = ChronexExpression.Parse("0 0 * * * {max:100}");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from, 3).ToList();
        occurrences.Count.Should().Be(3);
    }

    [Fact]
    public void Enumerate_WithFrom()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {from:2026-06-01}");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from, 1).ToList();
        occurrences.Count.Should().Be(1);
        occurrences[0].DateTime.Month.Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public void Enumerate_WeekdayOnly()
    {
        var expr = ChronexExpression.Parse("0 9 * * MON-FRI");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from, 5).ToList();
        foreach (var occ in occurrences)
        {
            occ.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
            occ.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        }
    }

    [Fact]
    public void Enumerate_Empty_WhenPast()
    {
        var expr = ChronexExpression.Parse("@once 2020-01-01T00:00:00Z");
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var occurrences = expr.Enumerate(from).ToList();
        occurrences.Should().BeEmpty();
    }
}
