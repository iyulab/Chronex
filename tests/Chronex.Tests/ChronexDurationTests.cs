using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class ChronexDurationTests
{
    [Theory]
    [InlineData("30s", 30_000)]
    [InlineData("5m", 300_000)]
    [InlineData("2h", 7_200_000)]
    [InlineData("1d", 86_400_000)]
    [InlineData("500ms", 500)]
    [InlineData("1h30m", 5_400_000)]
    [InlineData("1d2h30m15s500ms", 95_415_500)]
    public void Parse_ValidDurations(string input, long expectedMs)
    {
        var d = ChronexDuration.Parse(input);
        d.TotalMilliseconds.Should().Be(expectedMs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("30")]
    [InlineData("30x")]
    [InlineData("m")]
    public void TryParse_InvalidDurations_ReturnsFalse(string input)
    {
        ChronexDuration.TryParse(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("1h30m", "1h30m")]
    [InlineData("90m", "1h30m")]
    [InlineData("3600s", "1h")]
    [InlineData("500ms", "500ms")]
    [InlineData("86400000ms", "1d")]
    public void ToString_CanonicalForm(string input, string expected)
    {
        var d = ChronexDuration.Parse(input);
        d.ToString().Should().Be(expected);
    }

    [Fact]
    public void Equality_SameValue()
    {
        var a = ChronexDuration.Parse("1h30m");
        var b = ChronexDuration.Parse("90m");
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_ToTimeSpan()
    {
        var d = ChronexDuration.Parse("2h");
        TimeSpan ts = d;
        ts.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void TryParse_Overflow_ReturnsFalse()
    {
        // m-3: Overflow should return false, not produce garbage
        ChronexDuration.TryParse("999999999999999999d", out _).Should().BeFalse();
    }

    [Fact]
    public void Default_Struct_HasEmptyOriginal()
    {
        // m-9: default struct should not have null Original
        var d = default(ChronexDuration);
        d.Original.Should().NotBeNull();
        d.Original.Should().Be(string.Empty);
        d.Value.Should().Be(TimeSpan.Zero);
        d.ToString().Should().Be("0ms");
    }
}
