using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class ChronexExpressionTests
{
    // --- Cron parsing ---

    [Fact]
    public void Parse_StandardCron_5Field()
    {
        var expr = ChronexExpression.Parse("*/5 * * * *");
        expr.Kind.Should().Be(ScheduleKind.Cron);
        expr.Timezone.Should().BeNull();
        expr.ScheduleTimeZone.Should().BeNull();
        expr.CronSchedule.Should().NotBeNull();
        expr.OptionsRaw.Should().BeNull();
        expr.Matches(new DateTime(2026, 1, 1, 0, 5, 0)).Should().BeTrue();
        expr.Matches(new DateTime(2026, 1, 1, 0, 3, 0)).Should().BeFalse();
    }

    [Fact]
    public void Parse_StandardCron_6Field()
    {
        var expr = ChronexExpression.Parse("30 */5 * * * *");
        expr.Kind.Should().Be(ScheduleKind.Cron);
        expr.CronSchedule.Should().NotBeNull();
        expr.CronSchedule!.HasSeconds.Should().BeTrue();
    }

    // --- Timezone ---

    [Fact]
    public void Parse_WithTimezone()
    {
        var expr = ChronexExpression.Parse("TZ=UTC 0 9 * * MON-FRI");
        expr.Timezone.Should().Be("UTC");
        expr.ScheduleTimeZone.Should().NotBeNull();
        expr.ScheduleTimeZone!.Id.Should().Be("UTC");
        expr.Kind.Should().Be(ScheduleKind.Cron);
    }

    [Fact]
    public void Parse_InvalidTimezone_Fails()
    {
        ChronexExpression.TryParse("TZ=Fake/Zone 0 0 * * *", out _, out var error)
            .Should().BeFalse();
        error!.Should().Contain("Unknown timezone");
    }

    // --- Aliases ---

    [Theory]
    [InlineData("@daily")]
    [InlineData("@midnight")]
    [InlineData("@hourly")]
    public void Parse_Alias(string alias)
    {
        var expr = ChronexExpression.Parse(alias);
        expr.Kind.Should().Be(ScheduleKind.Alias);
        expr.CronSchedule.Should().NotBeNull();
        // Verify the alias resolves correctly via matching
        if (alias is "@daily" or "@midnight")
        {
            expr.Matches(new DateTime(2026, 1, 1, 0, 0, 0)).Should().BeTrue();
            expr.Matches(new DateTime(2026, 1, 1, 1, 0, 0)).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("@yearly")]
    [InlineData("@annually")]
    public void Parse_Alias_Yearly(string alias)
    {
        var expr = ChronexExpression.Parse(alias);
        expr.Kind.Should().Be(ScheduleKind.Alias);
        // Jan 1 00:00
        expr.Matches(new DateTime(2026, 1, 1, 0, 0, 0)).Should().BeTrue();
        expr.Matches(new DateTime(2026, 2, 1, 0, 0, 0)).Should().BeFalse();
    }

    [Fact]
    public void Parse_Alias_Monthly()
    {
        var expr = ChronexExpression.Parse("@monthly");
        expr.Matches(new DateTime(2026, 3, 1, 0, 0, 0)).Should().BeTrue();
        expr.Matches(new DateTime(2026, 3, 2, 0, 0, 0)).Should().BeFalse();
    }

    [Fact]
    public void Parse_Alias_Weekly()
    {
        var expr = ChronexExpression.Parse("@weekly");
        // Sunday 00:00
        expr.Matches(new DateTime(2026, 1, 4, 0, 0, 0)).Should().BeTrue();  // Sunday
        expr.Matches(new DateTime(2026, 1, 5, 0, 0, 0)).Should().BeFalse(); // Monday
    }

    [Fact]
    public void Parse_UnknownAlias_Fails()
    {
        ChronexExpression.TryParse("@biweekly", out _, out var error)
            .Should().BeFalse();
        error!.Should().Contain("Unknown alias");
    }

    // --- Aliases with TZ and options ---

    [Fact]
    public void Parse_AliasWithTimezoneAndOptions()
    {
        var expr = ChronexExpression.Parse("TZ=UTC @daily {jitter:5m}");
        expr.Kind.Should().Be(ScheduleKind.Alias);
        expr.Timezone.Should().Be("UTC");
        expr.OptionsRaw.Should().Be("jitter:5m");
        expr.CronSchedule.Should().NotBeNull();
    }

    // --- Options block ---

    [Fact]
    public void Parse_WithOptions()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {jitter:30s, until:2025-12-31}");
        expr.OptionsRaw.Should().Be("jitter:30s, until:2025-12-31");
        expr.Kind.Should().Be(ScheduleKind.Cron);
    }

    // --- Interval/Once (kind detection, parsing deferred to Cycle 06) ---

    [Fact]
    public void Parse_Interval_Kind()
    {
        var expr = ChronexExpression.Parse("@every 30m");
        expr.Kind.Should().Be(ScheduleKind.Interval);
        expr.CronSchedule.Should().BeNull();
    }

    [Fact]
    public void Parse_Once_Kind()
    {
        var expr = ChronexExpression.Parse("@once 2025-03-01T09:00:00+09:00");
        expr.Kind.Should().Be(ScheduleKind.Once);
        expr.CronSchedule.Should().BeNull();
    }

    // --- Error handling ---

    [Fact]
    public void Parse_Empty_Throws()
    {
        FluentActions.Invoking(() => ChronexExpression.Parse("")).Should().Throw<FormatException>();
    }

    [Fact]
    public void TryParse_InvalidCron_ReturnsFalse()
    {
        ChronexExpression.TryParse("invalid cron", out _, out var error)
            .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Matches_Interval_ReturnsFalse()
    {
        var expr = ChronexExpression.Parse("@every 5m");
        // Interval expressions are time-relative, not point-matching
        expr.Matches(new DateTime(2026, 1, 1, 0, 5, 0)).Should().BeFalse();
    }

    [Fact]
    public void Matches_Once_ReturnsFalse()
    {
        var expr = ChronexExpression.Parse("@once 2026-03-01T09:00:00+00:00");
        // Once expressions are time-relative, not point-matching
        expr.Matches(new DateTime(2026, 3, 1, 9, 0, 0)).Should().BeFalse();
    }
}
