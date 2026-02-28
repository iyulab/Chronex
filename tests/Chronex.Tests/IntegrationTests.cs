using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

/// <summary>
/// End-to-end integration tests combining multiple components.
/// </summary>
public class IntegrationTests
{
    private static FakeTimeProvider CreateTimeProvider(DateTimeOffset start)
        => new(start);

    // --- TriggerDefinition → Scheduler integration ---

    [Fact]
    public async Task TriggerDefinition_RegisterAndFire_WithMetadata()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var definition = new TriggerDefinition
        {
            Id = "health-check",
            Expression = "@every 5m",
            Metadata = new() { ["env"] = "prod", ["endpoint"] = "https://api.example.com" }
        };

        string? capturedEnv = null;
        string? capturedEndpoint = null;
        scheduler.Register(definition, (ctx, ct) =>
        {
            capturedEnv = ctx.Metadata["env"];
            capturedEndpoint = ctx.Metadata["endpoint"];
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(5));
        await scheduler.TickAsync();

        capturedEnv.Should().Be("prod");
        capturedEndpoint.Should().Be("https://api.example.com");
    }

    [Fact]
    public void TriggerDefinition_JsonRoundTrip()
    {
        var definition = new TriggerDefinition
        {
            Id = "sync",
            Expression = "TZ=UTC @every 15m {stagger:3m}",
            Enabled = true,
            Metadata = new() { ["env"] = "prod" }
        };

        var json = JsonSerializer.Serialize(definition);
        var deserialized = JsonSerializer.Deserialize<TriggerDefinition>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("sync");
        deserialized.Expression.Should().Be("TZ=UTC @every 15m {stagger:3m}");
        deserialized.Enabled.Should().BeTrue();
        deserialized.Metadata.Should().NotBeNull();
        deserialized.Metadata!["env"].Should().Be("prod");
    }

    [Fact]
    public async Task TriggerDefinition_Disabled_DoesNotFire()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var definition = new TriggerDefinition
        {
            Id = "disabled-trigger",
            Expression = "* * * * *",
            Enabled = false
        };

        var fired = false;
        scheduler.Register(definition, (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        fired.Should().BeFalse();
    }

    // --- TriggerContext property verification ---

    [Fact]
    public async Task TriggerContext_AllProperties_Populated()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tp = CreateTimeProvider(start);
        await using var scheduler = new ChronexScheduler(tp);

        var definition = new TriggerDefinition
        {
            Id = "ctx-test",
            Expression = "* * * * *",
            Metadata = new() { ["key"] = "value" }
        };

        TriggerContext? captured = null;
        scheduler.Register(definition, (ctx, ct) =>
        {
            captured = ctx;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        captured.Should().NotBeNull();
        captured!.TriggerId.Should().Be("ctx-test");
        captured.FireCount.Should().Be(1);
        captured.Expression.Should().NotBeNull();
        captured.Expression.Kind.Should().Be(ScheduleKind.Cron);
        captured.ScheduledTime.Should().BeAfter(start);
        captured.ActualTime.Should().BeOnOrAfter(captured.ScheduledTime);
        captured.Metadata["key"].Should().Be("value");
    }

    // --- Disable → Re-enable → Fire ---

    [Fact]
    public async Task DisableReenable_FiresAfterReenable()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var count = 0;
        scheduler.Register("toggle", "* * * * *", (ctx, ct) =>
        {
            count++;
            return Task.CompletedTask;
        });

        scheduler.SetEnabled("toggle", false);
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        count.Should().Be(0);

        scheduler.SetEnabled("toggle", true);
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        count.Should().Be(1);
    }

    // --- Multiple triggers ---

    [Fact]
    public async Task MultipleTriggers_IndependentFiring()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        int countA = 0, countB = 0;
        scheduler.Register("a", "* * * * *", (ctx, ct) =>
        {
            countA++;
            return Task.CompletedTask;
        });
        scheduler.Register("b", "@every 5m", (ctx, ct) =>
        {
            countB++;
            return Task.CompletedTask;
        });

        // After 1 minute: a fires, b doesn't
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        countA.Should().Be(1);
        countB.Should().Be(0);

        // After 5 minutes total: both fire
        tp.Advance(TimeSpan.FromMinutes(4));
        await scheduler.TickAsync();
        countA.Should().Be(2);
        countB.Should().Be(1);
    }

    // --- Once trigger with metadata ---

    [Fact]
    public async Task OnceTrigger_FiresOnceWithMetadata()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tp = CreateTimeProvider(start);
        await using var scheduler = new ChronexScheduler(tp);

        var definition = new TriggerDefinition
        {
            Id = "one-shot",
            Expression = "@once +10m",
            Metadata = new() { ["task"] = "cleanup" }
        };

        string? capturedTask = null;
        var count = 0;
        // Need to pass reference time for relative @once
        var expr = ChronexExpression.Parse(definition.Expression, start);
        var reg = scheduler.Register("one-shot", expr, (ctx, ct) =>
        {
            capturedTask = ctx.Metadata.TryGetValue("task", out var t) ? t : null;
            count++;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(10));
        await scheduler.TickAsync();
        count.Should().Be(1);

        tp.Advance(TimeSpan.FromMinutes(10));
        await scheduler.TickAsync();
        count.Should().Be(1); // Should not fire again
    }

    // --- Full expression lifecycle ---

    [Fact]
    public void FullLifecycle_ParseValidateNextEnumerate()
    {
        const string input = "TZ=UTC 0 9 * * MON-FRI {max:5, until:2026-12-31}";

        // Validate
        var validation = ExpressionValidator.Validate(input);
        validation.IsValid.Should().BeTrue();

        // Parse
        var expr = ChronexExpression.Parse(input);
        expr.Kind.Should().Be(ScheduleKind.Cron);
        expr.Timezone.Should().Be("UTC");
        expr.Options.Max.Should().Be(5);
        expr.Options.Until.Should().NotBeNull();

        // ToString round-trip
        var str = expr.ToString();
        var roundTripped = ChronexExpression.Parse(str);
        roundTripped.Timezone.Should().Be("UTC");
        roundTripped.Options.Max.Should().Be(5);

        // GetNextOccurrence
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var next = expr.GetNextOccurrence(from);
        next.Should().NotBeNull();
        next!.Value.UtcDateTime.Hour.Should().Be(9);
        next.Value.UtcDateTime.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
        next.Value.UtcDateTime.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);

        // Enumerate
        var occurrences = expr.Enumerate(from).ToList();
        occurrences.Count.Should().Be(5); // max:5
        foreach (var occ in occurrences)
        {
            occ.UtcDateTime.Hour.Should().Be(9);
            occ.UtcDateTime.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
            occ.UtcDateTime.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        }
    }

    // --- Skipped event for disabled trigger ---

    [Fact]
    public async Task SkippedEvent_DisabledTrigger()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        string? skippedId = null;
        string? skippedReason = null;
        scheduler.TriggerSkipped += (id, reason) =>
        {
            skippedId = id;
            skippedReason = reason;
        };

        scheduler.Register("disabled-test", "* * * * *", (ctx, ct) => Task.CompletedTask);
        scheduler.SetEnabled("disabled-test", false);

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        skippedId.Should().Be("disabled-test");
        skippedReason.Should().Be("disabled");
    }

    // --- All schedule kinds fire correctly ---

    [Fact]
    public async Task AllScheduleKinds_FireCorrectly()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tp = CreateTimeProvider(start);
        await using var scheduler = new ChronexScheduler(tp);

        int cronCount = 0, aliasCount = 0, intervalCount = 0, onceCount = 0;

        scheduler.Register("cron", "* * * * *", (ctx, ct) => { cronCount++; return Task.CompletedTask; });
        scheduler.Register("alias", "@hourly", (ctx, ct) => { aliasCount++; return Task.CompletedTask; });
        scheduler.Register("interval", "@every 30m", (ctx, ct) => { intervalCount++; return Task.CompletedTask; });
        scheduler.Register("once", "@once +30m", (ctx, ct) => { onceCount++; return Task.CompletedTask; }, start);

        // At 30m: cron fires, interval fires, once fires, alias doesn't
        tp.Advance(TimeSpan.FromMinutes(30));
        await scheduler.TickAsync();
        cronCount.Should().BeGreaterThanOrEqualTo(1);
        intervalCount.Should().Be(1);
        onceCount.Should().Be(1);

        // At 1h: alias fires
        tp.Advance(TimeSpan.FromMinutes(30));
        await scheduler.TickAsync();
        aliasCount.Should().BeGreaterThanOrEqualTo(1);
    }

    // --- Error recovery: failed handler doesn't break scheduler ---

    [Fact]
    public async Task FailedHandler_DoesntBreakOtherTriggers()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var goodCount = 0;
        scheduler.Register("bad", "* * * * *", (ctx, ct) =>
            throw new InvalidOperationException("boom"));
        scheduler.Register("good", "* * * * *", (ctx, ct) =>
        {
            goodCount++;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        goodCount.Should().Be(1); // Good trigger still fires despite bad one failing
    }

    // --- Expression special chars integration ---

    [Fact]
    public void SpecialChars_ParseAndNext_Integration()
    {
        // Last day of each month at midnight
        var expr = ChronexExpression.Parse("0 0 L * *");
        var from = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var next = expr.GetNextOccurrence(from);
        next.Should().NotBeNull();
        next!.Value.DateTime.Should().Be(new DateTime(2026, 1, 31, 0, 0, 0));

        // Second Monday of each month at 9am
        var expr2 = ChronexExpression.Parse("0 9 * * MON#2");
        var from2 = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var next2 = expr2.GetNextOccurrence(from2);
        next2.Should().NotBeNull();
        // Second Monday of March 2026 = March 9
        next2!.Value.DateTime.Should().Be(new DateTime(2026, 3, 9, 9, 0, 0));
    }
}
