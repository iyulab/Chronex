using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class ExpressionTokenizerTests
{
    [Fact]
    public void Tokenize_SimpleCron()
    {
        var t = ExpressionTokenizer.Tokenize("*/5 * * * *");
        t.Kind.Should().Be(ScheduleKind.Cron);
        t.Timezone.Should().BeNull();
        t.Body.Should().Be("*/5 * * * *");
        t.OptionsRaw.Should().BeNull();
    }

    [Fact]
    public void Tokenize_WithTimezone()
    {
        var t = ExpressionTokenizer.Tokenize("TZ=Asia/Seoul 0 9 * * MON-FRI");
        t.Timezone.Should().Be("Asia/Seoul");
        t.Body.Should().Be("0 9 * * MON-FRI");
        t.Kind.Should().Be(ScheduleKind.Cron);
    }

    [Fact]
    public void Tokenize_WithOptions()
    {
        var t = ExpressionTokenizer.Tokenize("0 9 * * * {jitter:30s, until:2025-12-31}");
        t.Body.Should().Be("0 9 * * *");
        t.OptionsRaw.Should().Be("jitter:30s, until:2025-12-31");
        t.Kind.Should().Be(ScheduleKind.Cron);
    }

    [Fact]
    public void Tokenize_FullExpression()
    {
        var t = ExpressionTokenizer.Tokenize("TZ=UTC 0 9 * * MON-FRI {jitter:30s}");
        t.Timezone.Should().Be("UTC");
        t.Body.Should().Be("0 9 * * MON-FRI");
        t.OptionsRaw.Should().Be("jitter:30s");
    }

    [Fact]
    public void Tokenize_EveryInterval()
    {
        var t = ExpressionTokenizer.Tokenize("@every 30m");
        t.Kind.Should().Be(ScheduleKind.Interval);
        t.Body.Should().Be("@every 30m");
    }

    [Fact]
    public void Tokenize_Once()
    {
        var t = ExpressionTokenizer.Tokenize("@once 2025-03-01T09:00:00+09:00");
        t.Kind.Should().Be(ScheduleKind.Once);
        t.Body.Should().Be("@once 2025-03-01T09:00:00+09:00");
    }

    [Fact]
    public void Tokenize_OnceRelative()
    {
        var t = ExpressionTokenizer.Tokenize("@once +20m");
        t.Kind.Should().Be(ScheduleKind.Once);
        t.Body.Should().Be("@once +20m");
    }

    [Fact]
    public void Tokenize_Alias()
    {
        var t = ExpressionTokenizer.Tokenize("@daily");
        t.Kind.Should().Be(ScheduleKind.Alias);
        t.Body.Should().Be("@daily");
    }

    [Fact]
    public void Tokenize_AliasWithTimezoneAndOptions()
    {
        var t = ExpressionTokenizer.Tokenize("TZ=Asia/Seoul @daily {jitter:5m}");
        t.Timezone.Should().Be("Asia/Seoul");
        t.Body.Should().Be("@daily");
        t.OptionsRaw.Should().Be("jitter:5m");
        t.Kind.Should().Be(ScheduleKind.Alias);
    }

    [Fact]
    public void Tokenize_Empty_Throws()
    {
        FluentActions.Invoking(() => ExpressionTokenizer.Tokenize("")).Should().Throw<FormatException>();
    }

    [Fact]
    public void Tokenize_TrailingContentAfterOptions_Throws()
    {
        // m-7: Content after closing brace should be rejected
        var act = () => ExpressionTokenizer.Tokenize("0 9 * * * {jitter:30s} extra");
        act.Should().Throw<FormatException>().WithMessage("*Unexpected content after options block*");
    }

    [Fact]
    public void Tokenize_UnmatchedClosingBrace_Throws()
    {
        var act = () => ExpressionTokenizer.Tokenize("0 9 * * * }");
        act.Should().Throw<FormatException>().WithMessage("*Unmatched closing brace*");
    }

    [Fact]
    public void Tokenize_UnmatchedOpeningBrace_Throws()
    {
        var act = () => ExpressionTokenizer.Tokenize("0 9 * * * {jitter:30s");
        act.Should().Throw<FormatException>().WithMessage("*Unmatched opening brace*");
    }

    [Fact]
    public void Tokenize_TzOnly_Throws()
    {
        // TZ= prefix without schedule body
        FluentActions.Invoking(() => ExpressionTokenizer.Tokenize("TZ=UTC"))
            .Should().Throw<FormatException>();
    }
}
