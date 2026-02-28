using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class ScheduleOptionsTests
{
    [Fact]
    public void Parse_Jitter()
    {
        var opts = ScheduleOptions.Parse("jitter:30s");
        opts.Jitter.Should().NotBeNull();
        opts.Jitter!.Value.Value.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Parse_Stagger()
    {
        var opts = ScheduleOptions.Parse("stagger:3m");
        opts.Stagger.Should().NotBeNull();
        opts.Stagger!.Value.Value.Should().Be(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void Parse_Window()
    {
        var opts = ScheduleOptions.Parse("window:5m");
        opts.Window.Should().NotBeNull();
        opts.Window!.Value.Value.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Parse_Max()
    {
        var opts = ScheduleOptions.Parse("max:10");
        opts.Max.Should().Be(10);
    }

    [Fact]
    public void Parse_Tag_Single()
    {
        var opts = ScheduleOptions.Parse("tag:report");
        opts.Tags.Should().NotBeNull();
        opts.Tags!.Count.Should().Be(1);
        opts.Tags[0].Should().Be("report");
    }

    [Fact]
    public void Parse_Tag_Multiple()
    {
        var opts = ScheduleOptions.Parse("tag:report+daily");
        opts.Tags.Should().NotBeNull();
        opts.Tags!.Count.Should().Be(2);
        opts.Tags[0].Should().Be("report");
        opts.Tags[1].Should().Be("daily");
    }

    [Fact]
    public void Parse_FromDate()
    {
        var opts = ScheduleOptions.Parse("from:2025-06-01");
        opts.From.Should().NotBeNull();
        opts.From!.Value.Year.Should().Be(2025);
        opts.From.Value.Month.Should().Be(6);
        opts.From.Value.Day.Should().Be(1);
    }

    [Fact]
    public void Parse_UntilDate()
    {
        var opts = ScheduleOptions.Parse("until:2025-12-31");
        opts.Until.Should().NotBeNull();
        opts.Until!.Value.Year.Should().Be(2025);
        opts.Until.Value.Month.Should().Be(12);
        opts.Until.Value.Day.Should().Be(31);
        // date-only until should be end of day
        opts.Until.Value.Hour.Should().Be(23);
        opts.Until.Value.Minute.Should().Be(59);
    }

    [Fact]
    public void Parse_FromDateTime()
    {
        var opts = ScheduleOptions.Parse("from:2025-06-01T09:00:00Z");
        opts.From.Should().NotBeNull();
        opts.From!.Value.Hour.Should().Be(9);
    }

    [Fact]
    public void Parse_Multiple()
    {
        var opts = ScheduleOptions.Parse("jitter:30s, until:2025-12-31");
        opts.Jitter.Should().NotBeNull();
        opts.Until.Should().NotBeNull();
    }

    [Fact]
    public void Parse_Complex()
    {
        var opts = ScheduleOptions.Parse("jitter:5m, stagger:3m, window:10m, max:100, tag:health-check");
        opts.Jitter!.Value.Value.Should().Be(TimeSpan.FromMinutes(5));
        opts.Stagger!.Value.Value.Should().Be(TimeSpan.FromMinutes(3));
        opts.Window!.Value.Value.Should().Be(TimeSpan.FromMinutes(10));
        opts.Max.Should().Be(100);
        opts.Tags!.Count.Should().Be(1);
        opts.Tags[0].Should().Be("health-check");
    }

    [Fact]
    public void Parse_Empty_ReturnsDefaults()
    {
        var opts = ScheduleOptions.Parse("");
        opts.Jitter.Should().BeNull();
        opts.Max.Should().BeNull();
        opts.Tags.Should().BeNull();
    }

    [Fact]
    public void Parse_UnknownOption_Fails()
    {
        ScheduleOptions.TryParse("foo:bar", out _, out var error).Should().BeFalse();
        error!.Should().Contain("unknown option");
    }

    [Fact]
    public void Parse_InvalidMax_Fails()
    {
        ScheduleOptions.TryParse("max:abc", out _, out var error).Should().BeFalse();
        error!.Should().Contain("invalid max");
    }

    [Fact]
    public void Parse_MaxZero_Fails()
    {
        ScheduleOptions.TryParse("max:0", out _, out var error).Should().BeFalse();
        error!.Should().Contain("invalid max");
    }

    [Fact]
    public void ToString_RoundTrip()
    {
        var opts = ScheduleOptions.Parse("jitter:30s, max:10, tag:report+daily");
        var str = opts.ToString();
        str.Should().Contain("jitter:30s");
        str.Should().Contain("max:10");
        str.Should().Contain("tag:report+daily");
    }

    [Fact]
    public void ToString_UntilDateOnly_ShortFormat()
    {
        // date-only until (23:59:59.999) should produce short format
        var opts = ScheduleOptions.Parse("until:2025-12-31");
        var str = opts.ToString();
        str.Should().Be("until:2025-12-31");
    }

    [Fact]
    public void ToString_UntilExplicitTime_FullIso()
    {
        // Explicit time 23:59:59.000 (not .999) should NOT be treated as date-only
        var opts = new ScheduleOptions
        {
            Until = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero)
        };
        var str = opts.ToString();
        // Should produce full ISO format because Millisecond != 999
        str.Should().Contain("T");
        str.Should().NotBe("until:2025-12-31");
    }

    // Integration: via ChronexExpression

    [Fact]
    public void ChronexExpression_OptionsAreParsed()
    {
        var expr = ChronexExpression.Parse("0 9 * * * {jitter:30s, until:2025-12-31}");
        expr.Options.Jitter.Should().NotBeNull();
        expr.Options.Until.Should().NotBeNull();
    }

    [Fact]
    public void ChronexExpression_NoOptions_EmptyOptions()
    {
        var expr = ChronexExpression.Parse("0 9 * * *");
        expr.Options.Should().NotBeNull();
        expr.Options.Jitter.Should().BeNull();
    }

    [Fact]
    public void ChronexExpression_InvalidOptions_Fails()
    {
        ChronexExpression.TryParse("0 9 * * * {foo:bar}", out _, out var error)
            .Should().BeFalse();
        error!.Should().Contain("unknown option");
    }
}
