using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class ExpressionValidatorTests
{
    [Fact]
    public void Validate_DuplicateTags_WarnsW001()
    {
        // m-6: Duplicate tags within a single tag: option (+ separated)
        var result = ExpressionValidator.Validate("0 9 * * * {tag:foo+bar+foo}");
        result.Warnings.Should().Contain(w => w.Code == "W001" && w.Message.Contains("foo"));
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NoDuplicateTags_NoWarnings()
    {
        var result = ExpressionValidator.Validate("0 9 * * * {tag:foo+bar}");
        result.Warnings.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyExpression_ErrorE010()
    {
        var result = ExpressionValidator.Validate("");
        result.Errors.Should().Contain(e => e.Code == "E010");
    }

    [Fact]
    public void Validate_UnknownAlias_ErrorE010()
    {
        var result = ExpressionValidator.Validate("@bogus");
        result.Errors.Should().Contain(e => e.Code == "E010");
    }

    [Fact]
    public void Validate_InvalidCronFieldCount_ErrorE010()
    {
        var result = ExpressionValidator.Validate("* *");
        result.Errors.Should().Contain(e => e.Code == "E010");
    }

    [Fact]
    public void Validate_UnmatchedBrace_ErrorE010()
    {
        // m-7: Structural tokenizer errors surface as E010
        var result = ExpressionValidator.Validate("0 9 * * * {jitter:30s");
        result.Errors.Should().Contain(e => e.Code == "E010");
    }

    [Fact]
    public void Validate_ValidCron_NoErrors()
    {
        var result = ExpressionValidator.Validate("0 9 * * MON-FRI");
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_FromAfterUntil_ErrorE020()
    {
        var result = ExpressionValidator.Validate(
            "0 9 * * * {from:2025-12-31, until:2025-01-01}");
        result.Errors.Should().Contain(e => e.Code == "E020");
    }

    [Fact]
    public void Validate_JitterExceedsHalfInterval_WarnsW002()
    {
        // @every 10m with jitter:6m → 6m > 50% of 10m → W002 warning
        var result = ExpressionValidator.Validate("@every 10m {jitter:6m}");
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Code == "W002");
    }

    [Fact]
    public void Validate_JitterWithinHalfInterval_NoWarning()
    {
        // @every 10m with jitter:4m → 4m < 50% of 10m → no warning
        var result = ExpressionValidator.Validate("@every 10m {jitter:4m}");
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_StaggerExceedsInterval_WarnsW003()
    {
        // @every 10m with stagger:15m → 15m > 10m → W003 warning
        var result = ExpressionValidator.Validate("@every 10m {stagger:15m}");
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Code == "W003");
    }

    [Fact]
    public void Validate_StaggerWithinInterval_NoWarning()
    {
        // @every 10m with stagger:5m → 5m < 10m → no warning
        var result = ExpressionValidator.Validate("@every 10m {stagger:5m}");
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidationError_HasPositionProperty()
    {
        // spec §5.4: ValidationError.Position is nullable int
        var result = ExpressionValidator.Validate("* * * * * * * *");
        result.Errors.Count.Should().BeGreaterThan(0);
        // Position is null by default (not yet computed)
        result.Errors[0].Position.Should().BeNull();
    }
}
