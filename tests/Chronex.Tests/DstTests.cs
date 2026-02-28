using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

/// <summary>
/// T-2: DST spring-forward and fall-back tests (M-5 fix verification).
/// Uses US Eastern Time for testing.
/// </summary>
public class DstTests
{
    private static TimeZoneInfo GetEasternTz()
    {
        // Try IANA first (Linux), then Windows ID
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }

    private static string GetEasternTzId()
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            return "America/New_York";
        }
        catch (TimeZoneNotFoundException)
        {
            return "Eastern Standard Time";
        }
    }

    [Fact]
    public void Next_SpringForward_SkipsInvalidTime()
    {
        // America/New_York: 2026-03-08 02:00 → 03:00 (spring-forward)
        var tzId = GetEasternTzId();
        var expr = ChronexExpression.Parse($"TZ={tzId} 30 2 * * *");
        // From before the gap
        var from = new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.FromHours(-5));
        var next = expr.GetNextOccurrence(from);

        // 02:30 doesn't exist — should skip to next valid occurrence
        next.Should().NotBeNull();

        // The result should NOT be UTC 07:30 (which would be invalid 02:30 EST)
        // It should be the next valid 02:30 (March 9th or later)
        var tz = GetEasternTz();
        var localTime = TimeZoneInfo.ConvertTime(next!.Value, tz);
        tz.IsInvalidTime(localTime.DateTime).Should().BeFalse(
            $"Result {next.Value} converts to invalid local time {localTime}");
    }

    [Fact]
    public void Next_FallBack_FiresOncePerDay()
    {
        // America/New_York: 2026-11-01 02:00 → 01:00 (fall-back)
        var tzId = GetEasternTzId();
        var expr = ChronexExpression.Parse($"TZ={tzId} 30 1 * * *");
        // From before fall-back
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var occurrences = expr.Enumerate(from, 2).ToList();

        occurrences.Count.Should().Be(2);
        // 01:30 exists twice (EDT and EST), but should fire only once per day
        var gap = occurrences[1] - occurrences[0];
        gap.TotalHours.Should().BeGreaterThan(23);
    }

    [Fact]
    public void ResolveLocalTime_NormalTime_ReturnsCorrectOffset()
    {
        var tzId = GetEasternTzId();
        var expr = ChronexExpression.Parse($"TZ={tzId} 0 12 * * *");
        // July 1st 2026 — EDT (UTC-4)
        var from = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var next = expr.GetNextOccurrence(from);

        next.Should().NotBeNull();
        var tz = GetEasternTz();
        var local = TimeZoneInfo.ConvertTime(next!.Value, tz);
        local.Hour.Should().Be(12);
    }

    [Fact]
    public void Next_SpringForward_HourlySchedule_SkipsGap()
    {
        // Hourly schedule during spring-forward
        var tzId = GetEasternTzId();
        var expr = ChronexExpression.Parse($"TZ={tzId} 0 * * * *");
        // From 01:00 on spring-forward day
        var from = new DateTimeOffset(2026, 3, 8, 1, 0, 0, TimeSpan.FromHours(-5));
        var occurrences = expr.Enumerate(from, 3).ToList();

        occurrences.Count.Should().Be(3);
        var tz = GetEasternTz();
        foreach (var occ in occurrences)
        {
            var local = TimeZoneInfo.ConvertTime(occ, tz);
            tz.IsInvalidTime(local.DateTime).Should().BeFalse(
                $"Occurrence {occ} at local {local} is in DST gap");
        }
    }

    [Fact]
    public void Next_FallBack_IntervalExpression_ContinuesAfterFallBack()
    {
        // Interval expressions may fire twice during fall-back (spec §3.5)
        // Verify that the expression produces valid occurrences through the transition
        var tzId = GetEasternTzId();
        var expr = ChronexExpression.Parse($"TZ={tzId} @every 30m");
        // From before fall-back (2026-11-01 01:00 EDT = UTC-4)
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var occurrences = expr.Enumerate(from, 10).ToList();

        occurrences.Count.Should().Be(10);
        // All occurrences should be strictly increasing
        for (var i = 1; i < occurrences.Count; i++)
        {
            occurrences[i].Should().BeAfter(occurrences[i - 1]);
        }
    }

    [Fact]
    public void Next_WithTimezone_RespectsTimezone()
    {
        var tzId = GetEasternTzId();
        var expr = ChronexExpression.Parse($"TZ={tzId} 0 9 * * MON");
        // From Monday midnight UTC
        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var next = expr.GetNextOccurrence(from);

        next.Should().NotBeNull();
        var tz = GetEasternTz();
        var local = TimeZoneInfo.ConvertTime(next!.Value, tz);
        local.Hour.Should().Be(9);
        local.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }
}
