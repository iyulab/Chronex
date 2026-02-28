namespace Chronex;

/// <summary>
/// Represents a registered trigger in the scheduler.
/// </summary>
public sealed class TriggerRegistration
{
    /// <summary>Unique trigger identifier.</summary>
    public string Id { get; }

    /// <summary>The parsed expression for this trigger.</summary>
    public ChronexExpression Expression { get; }

    /// <summary>The handler to invoke when the trigger fires.</summary>
    internal Func<TriggerContext, CancellationToken, Task> Handler { get; }

    private readonly object _lock = new();
    private volatile bool _enabled = true;
    private DateTimeOffset? _nextFireTime;
    private DateTimeOffset? _lastFired;

    /// <summary>Whether this trigger is enabled.</summary>
    public bool Enabled
    {
        get => _enabled;
        internal set => _enabled = value;
    }

    /// <summary>The last time this trigger fired.</summary>
    public DateTimeOffset? LastFired
    {
        get { lock (_lock) { return _lastFired; } }
        internal set { lock (_lock) { _lastFired = value; } }
    }

    /// <summary>The next scheduled fire time.</summary>
    public DateTimeOffset? NextFireTime
    {
        get { lock (_lock) { return _nextFireTime; } }
        internal set { lock (_lock) { _nextFireTime = value; } }
    }

    // C-3: Backing field for Interlocked.Increment
    internal int _fireCount;

    /// <summary>Total number of times this trigger has fired.</summary>
    public int FireCount => Volatile.Read(ref _fireCount);

    /// <summary>Free-form metadata from the TriggerDefinition.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    internal TriggerRegistration(string id, ChronexExpression expression,
        Func<TriggerContext, CancellationToken, Task> handler,
        Dictionary<string, string>? metadata = null)
    {
        Id = id;
        Expression = expression;
        Handler = handler;
        Metadata = metadata?.AsReadOnly();
    }
}
