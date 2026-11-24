using TaskBoard.Domain;

namespace TaskBoard.Api.Infrastructure;

/// <summary>Receives events after they were persisted, to keep read models up to date.</summary>
public interface IProjection
{
    void Apply(object @event);
}

public sealed class AggregateRepository(IEventStore store, IEnumerable<IProjection> projections)
{
    public static string StreamName(Type aggregateType, Guid id) => $"{aggregateType.Name}-{id}";

    /// <summary>Loads an aggregate by replaying its stream, or null when the stream does not exist.</summary>
    public async Task<T?> LoadAsync<T>(Guid id, CancellationToken ct = default) where T : Aggregate, new()
    {
        var history = await store.ReadStreamAsync(StreamName(typeof(T), id), ct);
        if (history.Count == 0) return null;

        var aggregate = new T();
        aggregate.LoadFromHistory(history.Select(e => e.Data));
        return aggregate;
    }

    /// <summary>Appends the aggregate's pending events (optimistic concurrency on its version) and updates projections.</summary>
    public async Task SaveAsync(Aggregate aggregate, CancellationToken ct = default)
    {
        var changes = aggregate.GetChanges().ToList();
        if (changes.Count == 0) return;

        await store.AppendAsync(StreamName(aggregate.GetType(), aggregate.Id), aggregate.Version, changes, ct);
        aggregate.AcceptChanges();

        foreach (var @event in changes)
        {
            foreach (var projection in projections) projection.Apply(@event);
        }
    }
}
