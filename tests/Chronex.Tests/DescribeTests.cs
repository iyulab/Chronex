using Shouldly;
using Xunit;

namespace Chronex.Tests;

public class DescribeTests
{
    [Fact]
    public void Describe_EveryMinute()
    {
        var expr = ChronexExpression.Parse("* * * * *");
        expr.Describe().ShouldBe("Every minute");
    }

    [Fact]
    public void Describe_Every5Minutes()
    {
        var expr = ChronexExpression.Parse("*/5 * * * *");
        expr.Describe().ShouldBe("Every 5 minutes");
    }

    [Fact]
    public void Describe_SpecificTime()
    {
        var expr = ChronexExpression.Parse("0 9 * * *");
        expr.Describe().ShouldBe("At 09:00");
    }

    [Fact]
    public void Describe_SpecificTimeWithDow()
    {
        var expr = ChronexExpression.Parse("0 9 * * MON-FRI");
        expr.Describe().ShouldBe("At 09:00, Monday through Friday");
    }

    [Fact]
    public void Describe_WithSeconds()
    {
        var expr = ChronexExpression.Parse("30 0 9 * * *");
        expr.Describe().ShouldBe("At 09:00:30");
    }

    [Fact]
    public void Describe_EveryHourAt30()
    {
        var expr = ChronexExpression.Parse("30 * * * *");
        expr.Describe().ShouldBe("Every hour at minute 30");
    }

    [Fact]
    public void Describe_SpecificDom()
    {
        var expr = ChronexExpression.Parse("0 0 1 * *");
        expr.Describe().ShouldBe("At midnight on day 1");
    }

    [Fact]
    public void Describe_SpecificMonth()
    {
        var expr = ChronexExpression.Parse("0 0 1 1 *");
        expr.Describe().ShouldBe("At midnight on day 1 of January");
    }

    [Fact]
    public void Describe_SpecificDow()
    {
        var expr = ChronexExpression.Parse("0 0 * * 0");
        expr.Describe().ShouldBe("At midnight on Sunday");
    }

    [Fact]
    public void Describe_MultipleHours()
    {
        var expr = ChronexExpression.Parse("0 9,17 * * MON-FRI");
        expr.Describe().ShouldBe("At 09:00 and 17:00, Monday through Friday");
    }

    // === Aliases ===

    [Fact]
    public void Describe_AliasDaily()
    {
        // @daily → 0 0 * * * → midnight, all days
        var expr = ChronexExpression.Parse("@daily");
        expr.Describe().ShouldBe("At midnight");
    }

    [Fact]
    public void Describe_AliasHourly()
    {
        // @hourly → 0 * * * * → every hour at minute 0
        var expr = ChronexExpression.Parse("@hourly");
        expr.Describe().ShouldBe("Every hour at minute 0");
    }

    [Fact]
    public void Describe_AliasWeekly()
    {
        // @weekly → 0 0 * * 0 → midnight on Sunday
        var expr = ChronexExpression.Parse("@weekly");
        expr.Describe().ShouldBe("At midnight on Sunday");
    }

    [Fact]
    public void Describe_AliasMonthly()
    {
        // @monthly → 0 0 1 * * → midnight on day 1
        var expr = ChronexExpression.Parse("@monthly");
        expr.Describe().ShouldBe("At midnight on day 1");
    }

    [Fact]
    public void Describe_AliasYearly()
    {
        // @yearly → 0 0 1 1 * → midnight on day 1 of January
        var expr = ChronexExpression.Parse("@yearly");
        expr.Describe().ShouldBe("At midnight on day 1 of January");
    }

    // === Intervals ===

    [Fact]
    public void Describe_IntervalFixed()
    {
        var expr = ChronexExpression.Parse("@every 30m");
        expr.Describe().ShouldBe("Every 30m");
    }

    [Fact]
    public void Describe_IntervalRange()
    {
        var expr = ChronexExpression.Parse("@every 1h-2h");
        expr.Describe().ShouldBe("Every 1h to 2h (randomized)");
    }

    // === Once ===

    [Fact]
    public void Describe_OnceAbsolute()
    {
        var expr = ChronexExpression.Parse("@once 2025-03-01T09:00:00Z");
        expr.Describe().ShouldBe("Once at 2025-03-01 09:00 UTC");
    }

    [Fact]
    public void Describe_OnceRelative()
    {
        var expr = ChronexExpression.Parse("@once +20m",
            referenceTime: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        expr.Describe().ShouldBe("Once, 20m from reference time");
    }

    // === Timezone ===

    [Fact]
    public void Describe_WithTimezone()
    {
        var expr = ChronexExpression.Parse("TZ=Asia/Seoul 0 9 * * *");
        var desc = expr.Describe();
        desc.ShouldContain("(Asia/Seoul)");
        desc.ShouldBe("At 09:00 (Asia/Seoul)");
    }

    // === Options ===

    [Fact]
    public void Describe_WithJitter()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {jitter:30s}");
        var desc = expr.Describe();
        desc.ShouldContain("jitter");
        desc.ShouldBe("At 09:00, with up to 30s jitter");
    }

    [Fact]
    public void Describe_WithJitterAndUntil()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {jitter:30s, until:2025-12-31}");
        var desc = expr.Describe();
        desc.ShouldContain("jitter");
        desc.ShouldContain("until");
        desc.ShouldBe("At 09:00, with up to 30s jitter, until 2025-12-31");
    }

    [Fact]
    public void Describe_FullExpressionWithTzDowAndOptions()
    {
        var expr = ChronexExpression.Parse("TZ=Asia/Seoul 0 9 * * MON-FRI {jitter:30s}");
        var desc = expr.Describe();
        desc.ShouldContain("09:00");
        desc.ShouldContain("Monday through Friday");
        desc.ShouldContain("(Asia/Seoul)");
        desc.ShouldContain("jitter");
        desc.ShouldBe("At 09:00, Monday through Friday (Asia/Seoul), with up to 30s jitter");
    }

    // === Special entries ===

    [Fact]
    public void Describe_LastDayOfMonth()
    {
        var expr = ChronexExpression.Parse("0 0 L * *");
        var desc = expr.Describe();
        desc.ShouldContain("last day");
        desc.ShouldBe("At midnight on the last day of the month");
    }

    [Fact]
    public void Describe_LastWeekdayOfMonth()
    {
        var expr = ChronexExpression.Parse("0 0 LW * *");
        var desc = expr.Describe();
        desc.ShouldContain("last weekday");
        desc.ShouldBe("At midnight on the last weekday of the month");
    }

    [Fact]
    public void Describe_NearestWeekday()
    {
        var expr = ChronexExpression.Parse("0 0 15W * *");
        var desc = expr.Describe();
        desc.ShouldContain("nearest weekday");
        desc.ShouldBe("At midnight on the nearest weekday to day 15");
    }

    [Fact]
    public void Describe_NthDowOfMonth()
    {
        var expr = ChronexExpression.Parse("0 0 * * MON#2");
        var desc = expr.Describe();
        desc.ShouldContain("2nd Monday");
        desc.ShouldBe("At midnight on the 2nd Monday");
    }

    [Fact]
    public void Describe_LastFriday()
    {
        var expr = ChronexExpression.Parse("0 0 * * 5L");
        var desc = expr.Describe();
        desc.ShouldContain("last Friday");
        desc.ShouldBe("At midnight on the last Friday");
    }

    // === Additional option and special entry coverage ===

    [Fact]
    public void Describe_WithStagger()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {stagger:5m}");
        var desc = expr.Describe();
        desc.ShouldBe("At 09:00, with 5m stagger");
    }

    [Fact]
    public void Describe_WithWindow()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {window:10m}");
        var desc = expr.Describe();
        desc.ShouldBe("At 09:00, within 10m window");
    }

    [Fact]
    public void Describe_WithMaxExecutions()
    {
        var expr = ChronexExpression.Parse("@every 1h {max:10}");
        var desc = expr.Describe();
        desc.ShouldBe("Every 1h, max 10 executions");
    }

    [Fact]
    public void Describe_WithTags()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {tag:report+daily}");
        var desc = expr.Describe();
        desc.ShouldBe("At 09:00, tagged report, daily");
    }

    [Fact]
    public void Describe_LastDayOffset()
    {
        var expr = ChronexExpression.Parse("0 0 L-3 * *");
        var desc = expr.Describe();
        desc.ShouldBe("At midnight on 3 days before the last day of the month");
    }
}
