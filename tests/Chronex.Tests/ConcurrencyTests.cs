using FluentAssertions;
using Xunit;

namespace Chronex.Tests;

/// <summary>
/// T-3: Concurrency tests for ChronexScheduler (C-2 through C-5 fix verification).
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public async Task Start_DoubleCall_IsIdempotent()
    {
        await using var scheduler = new ChronexScheduler();
        scheduler.Register("test", "* * * * *", (ctx, ct) => Task.CompletedTask);

        // Two Start() calls should not create two tick loops
        scheduler.Start();
        scheduler.Start();

        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Start_ConcurrentCalls_OnlyOneLoopCreated()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fireCount = 0;
        scheduler.Register("test", "* * * * *", (ctx, ct) =>
        {
            Interlocked.Increment(ref fireCount);
            return Task.CompletedTask;
        });

        // Launch multiple Start() calls concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => scheduler.Start()))
            .ToArray();
        await Task.WhenAll(tasks);

        // Advance and tick — should fire at most once
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        fireCount.Should().BeLessThanOrEqualTo(1);
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Register_AfterStart_IsPickedUp()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        scheduler.Start();

        // Register after Start()
        var fired = false;
        scheduler.Register("late", "* * * * *", (ctx, ct) =>
        {
            fired = true;
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();

        fired.Should().BeTrue();
        await scheduler.StopAsync();
    }

    [Fact]
    public async Task Unregister_DuringTick_IsSafe()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fireCount = 0;
        scheduler.Register("self-remove", "* * * * *", (ctx, ct) =>
        {
            fireCount++;
            scheduler.Unregister("self-remove");
            return Task.CompletedTask;
        });

        tp.Advance(TimeSpan.FromMinutes(1));
        // Should not throw even though trigger unregisters itself during handler
        await scheduler.TickAsync();

        fireCount.Should().Be(1);
        scheduler.GetTrigger("self-remove").Should().BeNull();
    }

    [Fact]
    public async Task StopAsync_ThenRestart_Works()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        var fireCount = 0;
        scheduler.Register("test", "* * * * *", (ctx, ct) =>
        {
            fireCount++;
            return Task.CompletedTask;
        });

        scheduler.Start();
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        await scheduler.StopAsync();

        // Restart
        scheduler.Start();
        tp.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync();
        await scheduler.StopAsync();

        fireCount.Should().Be(2);
    }

    [Fact]
    public async Task DisposeAsync_ThenStart_ThrowsObjectDisposed()
    {
        var scheduler = new ChronexScheduler();
        await scheduler.DisposeAsync();

        FluentActions.Invoking(() => scheduler.Start()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var scheduler = new ChronexScheduler();
        scheduler.Register("test", "* * * * *", (ctx, ct) => Task.CompletedTask);
        scheduler.Start();

        await scheduler.DisposeAsync();
        await scheduler.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task FailedHandler_NoSubscriber_DoesNotThrow()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        // No TriggerFailed subscriber — C-4: should not swallow silently,
        // but should NOT crash the scheduler either
        scheduler.Register("test", "* * * * *", (ctx, ct) =>
            throw new InvalidOperationException("boom"));

        tp.Advance(TimeSpan.FromMinutes(1));
        // Should not throw
        await scheduler.TickAsync();

        // Trigger should still be scheduled for next occurrence
        var trigger = scheduler.GetTrigger("test");
        trigger.Should().NotBeNull();
        trigger!.FireCount.Should().Be(1);
    }

    [Fact]
    public async Task CancellationDuringHandler_PropagatesOCE()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var scheduler = new ChronexScheduler(tp);

        using var cts = new CancellationTokenSource();
        scheduler.Register("test", "* * * * *", async (ctx, ct) =>
        {
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
        });

        tp.Advance(TimeSpan.FromMinutes(1));

        // C-4: OperationCanceledException should propagate
        var act = async () => await scheduler.TickAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
