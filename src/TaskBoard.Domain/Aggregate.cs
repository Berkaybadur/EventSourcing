namespace TaskBoard.Domain;

/// <summary>
/// Base class for event-sourced aggregates. State is only ever changed by applying events:
/// commands call <see cref="Raise"/>, and <see cref="When"/> mutates state for each event,
/// both for new events and when rehydrating from history.
/// </summary>
public abstract class Aggregate
{
    private readonly List<object> _changes = [];

    public Guid Id { get; protected set; }

    /// <summary>Version of the last persisted event; -1 for an aggregate with no history.</summary>
    public long Version { get; private set; } = -1;

    protected abstract void When(object @event);

    protected void Raise(object @event)
    {
        When(@event);
        _changes.Add(@event);
    }

    public IReadOnlyList<object> GetChanges() => _changes;

    /// <summary>Marks pending changes as persisted (called by the repository after a successful append).</summary>
    public void AcceptChanges()
    {
        Version += _changes.Count;
        _changes.Clear();
    }

    public void LoadFromHistory(IEnumerable<object> history)
    {
        foreach (var @event in history)
        {
            When(@event);
            Version++;
        }
    }
}
