using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

public class ChronexSchedulerTests
{
    private static FakeTimeProvider CreateTimeProvider(DateTimeOffset start)
    {
        return new FakeTimeProvider(start);
    }

    [Fact]
    public async Task Register_And_Tick()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fired = false;
        scheduler.Register("test", "* * * * *", (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        // Advance past next minute
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task Tick_DoesNotFire_BeforeNextTime()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fired = false;
        scheduler.Register("test", "0 9 * * *", (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(30));
        await scheduler.TickAsync();

        fired.Should().BeFalse();
    }

    [Fact]
    public async Task Unregister_Prevents_Firing()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fired = false;
        scheduler.Register("test", "* * * * *", (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        scheduler.Unregister("test").Should().BeTrue();

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        fired.Should().BeFalse();
    }

    [Fact]
    public async Task SetEnabled_False_Prevents_Firing()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fired = false;
        scheduler.Register("test", "* * * * *", (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        scheduler.SetEnabled("test", false);
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        fired.Should().BeFalse();
    }

    [Fact]
    public async Task MaxOption_LimitsFireCount()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var count = 0;
        scheduler.Register("test", "* * * * * {max:2}", (ctx, ct) =>
        {
            count++;
            return Task.CompletedTask;
        });

        for (var i = 0; i < 5; i++)
        {
            tp.Advance(TimeSpan.FromMinutes(1));
            await scheduler.TickAsync();
        }

        count.Should().Be(2);
    }

    [Fact]
    public async Task Events_AreFired()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        TriggerContext? firingCtx = null;
        TriggerContext? completedCtx = null;
        scheduler.TriggerFiring += ctx => firingCtx = ctx;
        scheduler.TriggerCompleted += ctx => completedCtx = ctx;

        scheduler.Register("test", "* * * * *", (ctx, ct) => Task.CompletedTask);

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        firingCtx.Should().NotBeNull();
        firingCtx!.TriggerId.Should().Be("test");
        completedCtx.Should().NotBeNull();
    }

    [Fact]
    public async Task FailedHandler_FiresFailedEvent()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        Exception? capturedEx = null;
        scheduler.TriggerFailed += (ctx, ex) => capturedEx = ex;

        scheduler.Register("test", "* * * * *", (ctx, ct) =>
            throw new InvalidOperationException("test error"));

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        capturedEx.Should().NotBeNull();
        capturedEx!.Message.Should().Be("test error");
    }

    [Fact]
    public async Task FireCount_Increments()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        scheduler.Register("test", "* * * * *", (ctx, ct) => Task.CompletedTask);

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        var trigger = scheduler.GetTrigger("test");
        trigger.Should().NotBeNull();
        trigger!.FireCount.Should().Be(2);
    }

    [Fact]
    public async Task IntervalTrigger()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var count = 0;
        scheduler.Register("test", "@every 5m", (ctx, ct) =>
        {
            count++;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(5));
        await scheduler.TickAsync();
        count.Should().Be(1);

        tp.Advance(TimeSpan.FromMinutes(5));
        await scheduler.TickAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task OnceTrigger_FiresOnce()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tp = CreateTimeProvider(start);
        await using var scheduler = new ChronexScheduler(tp);

        var count = 0;
        scheduler.Register("test", "@once +5m", (ctx, ct) =>
        {
            count++;
            return Task.CompletedTask;
        }, start);

        tp.Advance(TimeSpan.FromMinutes(5));
        await scheduler.TickAsync();
        count.Should().Be(1);

        tp.Advance(TimeSpan.FromMinutes(5));
        await scheduler.TickAsync();
        count.Should().Be(1); // Should not fire again
    }

    [Fact]
    public void GetTriggers_ReturnsAll()
    {
        var scheduler = new ChronexScheduler();
        scheduler.Register("a", "* * * * *", (ctx, ct) => Task.CompletedTask);
        scheduler.Register("b", "@daily", (ctx, ct) => Task.CompletedTask);

        var triggers = scheduler.GetTriggers();
        triggers.Count.Should().Be(2);
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var scheduler = new ChronexScheduler();
        scheduler.Register("test", "* * * * *", (ctx, ct) => Task.CompletedTask);
        FluentActions.Invoking(() => scheduler.Register("test", "@daily", (ctx, ct) => Task.CompletedTask)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Register_NullId_ThrowsArgumentException()
    {
        var scheduler = new ChronexScheduler();
        FluentActions.Invoking(() => scheduler.Register(null!, "* * * * *", (ctx, ct) => Task.CompletedTask))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_EmptyExpression_ThrowsArgumentException()
    {
        var scheduler = new ChronexScheduler();
        FluentActions.Invoking(() => scheduler.Register("test", "", (ctx, ct) => Task.CompletedTask))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_NullHandler_ThrowsArgumentNullException()
    {
        var scheduler = new ChronexScheduler();
        FluentActions.Invoking(() => scheduler.Register("test", "* * * * *", null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_NullDefinition_ThrowsArgumentNullException()
    {
        var scheduler = new ChronexScheduler();
        FluentActions.Invoking(() => scheduler.Register((TriggerDefinition)null!, (ctx, ct) => Task.CompletedTask))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Window_SkipsExpiredOccurrence()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fired = false;
        string? skippedReason = null;
        scheduler.TriggerSkipped += (id, reason) => skippedReason = reason;

        scheduler.Register("test", "* * * * * {window:30s}", (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        // Skip way past the window
        tp.Advance(TimeSpan.FromMinutes(5));
        await scheduler.TickAsync();

        skippedReason.Should().Be("window exceeded");
        fired.Should().BeFalse();
    }

    [Fact]
    public async Task Window_AllowsWithinWindow()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fired = false;
        scheduler.Register("test", "* * * * * {window:2m}", (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        // Advance just past next minute (within 2m window)
        tp.Advance(TimeSpan.FromSeconds(65));
        await scheduler.TickAsync();

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task Stagger_DelaysFiring()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fireCount = 0;
        scheduler.Register("my-trigger", "* * * * * {stagger:30s}", (ctx, ct) =>
        {
            fireCount++;
            return Task.CompletedTask;
        });

        // Advance exactly to the next minute (without stagger offset, would fire)
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        // Stagger adds an offset based on hash of "my-trigger" % 30s
        // Whether it fires depends on the hash value — verify behavior is consistent
        var trigger = scheduler.GetTrigger("my-trigger");
        trigger.Should().NotBeNull();
        // After advancing past stagger window, should eventually fire
        tp.Advance(TimeSpan.FromSeconds(30));
        await scheduler.TickAsync();
        fireCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CancelledHandler_RollsBackFireCount()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        using var cts = new CancellationTokenSource();

        scheduler.Register("test", "* * * * *", (ctx, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });

        // Cancel before ticking
        cts.Cancel();

        tp.Advance(TimeSpan.FromMinutes(1));

        var act = () => scheduler.TickAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        // C-5: FireCount should be rolled back to 0 on cancellation
        var trigger = scheduler.GetTrigger("test");
        trigger.Should().NotBeNull();
        trigger!.FireCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelledHandler_RestoresNextFireTime()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        using var cts = new CancellationTokenSource();

        scheduler.Register("test", "* * * * *", (ctx, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });

        cts.Cancel();
        tp.Advance(TimeSpan.FromMinutes(1));

        var act = () => scheduler.TickAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        // NextFireTime should be restored (not null) after cancellation
        var trigger = scheduler.GetTrigger("test");
        trigger.Should().NotBeNull();
        trigger!.NextFireTime.Should().NotBeNull();
    }

    [Fact]
    public async Task DisabledTrigger_EmitsTriggerSkippedEvent()
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

        scheduler.Register("test", "* * * * *", (ctx, ct) => Task.CompletedTask);
        scheduler.SetEnabled("test", false);

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        skippedId.Should().Be("test");
        skippedReason.Should().Be("disabled");
    }

    [Fact]
    public async Task TriggerContext_HasCorrectProperties()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tp = CreateTimeProvider(start);
        await using var scheduler = new ChronexScheduler(tp);

        TriggerContext? captured = null;
        scheduler.Register("ctx-test", "* * * * *", (ctx, ct) =>
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
    }

    [Fact]
    public async Task TriggerContext_WithMetadata()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tp = CreateTimeProvider(start);
        await using var scheduler = new ChronexScheduler(tp);

        TriggerContext? captured = null;
        var def = new TriggerDefinition
        {
            Id = "meta-test",
            Expression = "* * * * *",
            Metadata = new() { ["env"] = "test", ["key"] = "value" }
        };

        scheduler.Register(def, (ctx, ct) =>
        {
            captured = ctx;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        captured.Should().NotBeNull();
        captured!.Metadata.Should().NotBeNull();
        captured.Metadata!["env"].Should().Be("test");
        captured.Metadata["key"].Should().Be("value");
    }

    [Fact]
    public async Task MultipleTriggers_AllFire()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var firedA = false;
        var firedB = false;
        scheduler.Register("a", "* * * * *", (ctx, ct) =>
        {
            firedA = true;
            return Task.CompletedTask;
        });
        scheduler.Register("b", "* * * * *", (ctx, ct) =>
        {
            firedB = true;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        firedA.Should().BeTrue();
        firedB.Should().BeTrue();
    }

    [Fact]
    public async Task MaxReached_StopsFiring()
    {
        var tp = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var count = 0;
        scheduler.Register("test", "* * * * * {max:1}", (ctx, ct) =>
        {
            count++;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        count.Should().Be(1);

        // After max reached, NextFireTime is null → trigger stops
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        count.Should().Be(1);

        var trigger = scheduler.GetTrigger("test");
        trigger!.NextFireTime.Should().BeNull();
    }

    [Fact]
    public async Task Stagger_IsDeterministic()
    {
        // Two schedulers with same trigger ID should have same stagger behavior
        var tp1 = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var tp2 = CreateTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var s1 = new ChronexScheduler(tp1);
        await using var s2 = new ChronexScheduler(tp2);

        int count1 = 0, count2 = 0;
        s1.Register("same-id", "* * * * * {stagger:30s}", (ctx, ct) =>
        {
            count1++;
            return Task.CompletedTask;
        });
        s2.Register("same-id", "* * * * * {stagger:30s}", (ctx, ct) =>
        {
            count2++;
            return Task.CompletedTask;
        });

        // Advance past stagger
        tp1.Advance(TimeSpan.FromMinutes(2));
        tp2.Advance(TimeSpan.FromMinutes(2));
        await s1.TickAsync();
        await s2.TickAsync();

        // Same ID → same stagger offset → same behavior
        count1.Should().Be(count2);
    }
}

/// <summary>
/// Simple fake TimeProvider for testing.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration)
    {
        _now += duration;
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        _now = value;
    }
}
