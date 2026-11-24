namespace TaskBoard.Api.Infrastructure;

/// <summary>Thread-safe in-memory event store, used for tests and local runs without a database.</summary>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly List<StoredEvent> _events = [];
    private readonly Lock _gate = new();

    public Task AppendAsync(string stream, long expectedVersion, IReadOnlyList<object> events, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var currentVersion = _events.Count(e => e.Stream == stream) - 1L;
            if (currentVersion != expectedVersion) throw new ConcurrencyException(stream, expectedVersion);

            var version = expectedVersion;
            foreach (var @event in events)
            {
                _events.Add(new StoredEvent(stream, ++version, @event, DateTimeOffset.UtcNow));
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(string stream, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredEvent>>(_events.Where(e => e.Stream == stream).ToList());
        }
    }

    public Task<IReadOnlyList<StoredEvent>> ReadAllAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredEvent>>(_events.ToList());
        }
    }
}
