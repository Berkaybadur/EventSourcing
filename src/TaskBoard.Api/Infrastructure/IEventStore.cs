namespace TaskBoard.Api.Infrastructure;

/// <summary>An event as read back from the store, with its position in the stream.</summary>
public sealed record StoredEvent(string Stream, long Version, object Data, DateTimeOffset Timestamp);

/// <summary>Thrown when a stream was modified by someone else since it was loaded.</summary>
public sealed class ConcurrencyException(string stream, long expectedVersion)
    : Exception($"Stream '{stream}' was modified concurrently (expected version {expectedVersion}).");

public interface IEventStore
{
    /// <summary>
    /// Appends events to a stream. <paramref name="expectedVersion"/> is the version of the last event the
    /// caller saw (-1 for a new stream); a mismatch raises <see cref="ConcurrencyException"/>.
    /// </summary>
    Task AppendAsync(string stream, long expectedVersion, IReadOnlyList<object> events, CancellationToken ct = default);

    Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(string stream, CancellationToken ct = default);

    /// <summary>Reads every event, used to rebuild read models.</summary>
    Task<IReadOnlyList<StoredEvent>> ReadAllAsync(CancellationToken ct = default);
}
