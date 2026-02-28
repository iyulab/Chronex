using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class CronFieldTests
{
    [Fact]
    public void Wildcard_MatchesAll()
    {
        var field = CronField.Parse("*", CronFieldType.Minute);
        field.IsWildcard.Should().BeTrue();
        field.Matches(0).Should().BeTrue();
        field.Matches(30).Should().BeTrue();
        field.Matches(59).Should().BeTrue();
    }

    [Fact]
    public void WildcardStep_MatchesMultiples()
    {
        var field = CronField.Parse("*/5", CronFieldType.Minute);
        field.Matches(0).Should().BeTrue();
        field.Matches(5).Should().BeTrue();
        field.Matches(55).Should().BeTrue();
        field.Matches(3).Should().BeFalse();
        field.Matches(58).Should().BeFalse();
    }

    [Fact]
    public void SingleValue_MatchesExact()
    {
        var field = CronField.Parse("30", CronFieldType.Minute);
        field.Matches(30).Should().BeTrue();
        field.Matches(0).Should().BeFalse();
        field.Matches(31).Should().BeFalse();
    }

    [Fact]
    public void Range_MatchesInclusive()
    {
        var field = CronField.Parse("9-17", CronFieldType.Hour);
        field.Matches(8).Should().BeFalse();
        field.Matches(9).Should().BeTrue();
        field.Matches(13).Should().BeTrue();
        field.Matches(17).Should().BeTrue();
        field.Matches(18).Should().BeFalse();
    }

    [Fact]
    public void ReversedRange_WrapsAround()
    {
        // 23-01 means 23,0,1
        var field = CronField.Parse("23-1", CronFieldType.Hour);
        field.Matches(23).Should().BeTrue();
        field.Matches(0).Should().BeTrue();
        field.Matches(1).Should().BeTrue();
        field.Matches(22).Should().BeFalse();
        field.Matches(2).Should().BeFalse();
    }

    [Fact]
    public void RangeWithStep()
    {
        var field = CronField.Parse("0-30/10", CronFieldType.Minute);
        field.Matches(0).Should().BeTrue();
        field.Matches(10).Should().BeTrue();
        field.Matches(20).Should().BeTrue();
        field.Matches(30).Should().BeTrue();
        field.Matches(5).Should().BeFalse();
        field.Matches(31).Should().BeFalse();
    }

    [Fact]
    public void CommaSeparatedList()
    {
        var field = CronField.Parse("1,15", CronFieldType.DayOfMonth);
        field.Matches(1).Should().BeTrue();
        field.Matches(15).Should().BeTrue();
        field.Matches(2).Should().BeFalse();
    }

    [Fact]
    public void DayOfWeek_NamedValues()
    {
        var field = CronField.Parse("MON-FRI", CronFieldType.DayOfWeek);
        field.Matches(1).Should().BeTrue();  // MON
        field.Matches(5).Should().BeTrue();  // FRI
        field.Matches(0).Should().BeFalse(); // SUN
        field.Matches(6).Should().BeFalse(); // SAT
    }

    [Fact]
    public void DayOfWeek_Seven_NormalizedToZero()
    {
        // Both 0 and 7 mean Sunday
        var field = CronField.Parse("7", CronFieldType.DayOfWeek);
        field.Matches(0).Should().BeTrue(); // normalized to 0
    }

    [Fact]
    public void Month_NamedValues()
    {
        var field = CronField.Parse("JAN,JUN,DEC", CronFieldType.Month);
        field.Matches(1).Should().BeTrue();
        field.Matches(6).Should().BeTrue();
        field.Matches(12).Should().BeTrue();
        field.Matches(2).Should().BeFalse();
    }

    [Fact]
    public void OutOfRange_ThrowsFormatException()
    {
        FluentActions.Invoking(() => CronField.Parse("25", CronFieldType.Hour)).Should().Throw<FormatException>();
    }

    [Fact]
    public void InvalidStep_ThrowsFormatException()
    {
        FluentActions.Invoking(() => CronField.Parse("*/0", CronFieldType.Minute)).Should().Throw<FormatException>();
    }

    [Fact]
    public void ReversedRange_DayOfWeek_FriToMon()
    {
        // FRI-MON = FRI,SAT,SUN,MON
        var field = CronField.Parse("FRI-MON", CronFieldType.DayOfWeek);
        field.Matches(5).Should().BeTrue();  // FRI
        field.Matches(6).Should().BeTrue();  // SAT
        field.Matches(0).Should().BeTrue();  // SUN
        field.Matches(1).Should().BeTrue();  // MON
        field.Matches(2).Should().BeFalse(); // TUE
        field.Matches(4).Should().BeFalse(); // THU
    }
}
