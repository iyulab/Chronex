using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class CronAliasTests
{
    [Theory]
    [InlineData("@yearly", "0", "0", "1", "1", "*")]
    [InlineData("@annually", "0", "0", "1", "1", "*")]
    [InlineData("@monthly", "0", "0", "1", "*", "*")]
    [InlineData("@weekly", "0", "0", "*", "*", "0")]
    [InlineData("@daily", "0", "0", "*", "*", "*")]
    [InlineData("@midnight", "0", "0", "*", "*", "*")]
    [InlineData("@hourly", "0", "*", "*", "*", "*")]
    public void TryResolve_KnownAliases(string alias, string m, string h, string dom, string mon, string dow)
    {
        CronAlias.TryResolve(alias, out var fields).Should().BeTrue();
        fields.Should().NotBeNull();
        fields!.Length.Should().Be(5);
        fields[0].Should().Be(m);
        fields[1].Should().Be(h);
        fields[2].Should().Be(dom);
        fields[3].Should().Be(mon);
        fields[4].Should().Be(dow);
    }

    [Theory]
    [InlineData("@DAILY")]
    [InlineData("@Daily")]
    public void TryResolve_CaseInsensitive(string alias)
    {
        CronAlias.TryResolve(alias, out var fields).Should().BeTrue();
        fields.Should().NotBeNull();
    }

    [Fact]
    public void TryResolve_Unknown_ReturnsFalse()
    {
        CronAlias.TryResolve("@biweekly", out var fields).Should().BeFalse();
        fields.Should().BeNull();
    }
}
