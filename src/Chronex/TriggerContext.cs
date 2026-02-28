namespace Chronex;

/// <summary>
/// Context passed to trigger handlers when a trigger fires.
/// </summary>
public sealed class TriggerContext
{
    /// <summary>The trigger ID.</summary>
    public string TriggerId { get; }

    /// <summary>The scheduled fire time (before jitter/stagger adjustments).</summary>
    public DateTimeOffset ScheduledTime { get; }

    /// <summary>The actual fire time (after jitter/stagger adjustments).</summary>
    public DateTimeOffset ActualTime { get; }

    /// <summary>The fire count (1-based: 1 for first fire, 2 for second, etc.).</summary>
    public int FireCount { get; }

    /// <summary>The parsed expression.</summary>
    public ChronexExpression Expression { get; }

    /// <summary>Free-form metadata from the TriggerDefinition. Empty if not provided.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>().AsReadOnly();

    internal TriggerContext(string triggerId, DateTimeOffset scheduledTime,
        DateTimeOffset actualTime, int fireCount, ChronexExpression expression,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        TriggerId = triggerId;
        ScheduledTime = scheduledTime;
        ActualTime = actualTime;
        FireCount = fireCount;
        Expression = expression;
        Metadata = metadata ?? EmptyMetadata;
    }

    /// <summary>
    /// Creates a <see cref="TriggerContext"/> for unit testing handler implementations.
    /// </summary>
    /// <param name="triggerId">The trigger identifier.</param>
    /// <param name="fireCount">The fire count (1-based). Defaults to 1.</param>
    /// <param name="scheduledTime">The scheduled time. Defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    public static TriggerContext ForTest(
        string triggerId,
        int fireCount = 1,
        DateTimeOffset? scheduledTime = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var now = scheduledTime ?? DateTimeOffset.UtcNow;
        var expr = ChronexExpression.Parse("@every 1m");
        return new TriggerContext(triggerId, now, now, fireCount, expr, metadata);
    }
}
