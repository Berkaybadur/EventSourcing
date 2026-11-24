using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace TaskBoard.Api.Infrastructure;

/// <summary>
/// MongoDB event store. One document per event; a unique index on (Stream, Version) gives
/// optimistic concurrency: two writers appending at the same version cannot both succeed.
/// </summary>
public sealed class MongoEventStore : IEventStore
{
    private sealed class EventDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        public string Stream { get; set; } = string.Empty;
        public long Version { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    private readonly IMongoCollection<EventDocument> _events;

    public MongoEventStore(IMongoDatabase database)
    {
        _events = database.GetCollection<EventDocument>("events");
        _events.Indexes.CreateOne(new CreateIndexModel<EventDocument>(
            Builders<EventDocument>.IndexKeys.Ascending(e => e.Stream).Ascending(e => e.Version),
            new CreateIndexOptions { Unique = true }));
    }

    public async Task AppendAsync(string stream, long expectedVersion, IReadOnlyList<object> events, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var documents = events.Select((@event, i) =>
        {
            var (type, json) = EventSerializer.Serialize(@event);
            return new EventDocument
            {
                Stream = stream,
                Version = expectedVersion + 1 + i,
                Type = type,
                Data = json,
                Timestamp = now
            };
        }).ToList();

        try
        {
            // Ordered insert: if another writer already took the first version, nothing is written.
            await _events.InsertManyAsync(documents, new InsertManyOptions { IsOrdered = true }, ct);
        }
        catch (MongoBulkWriteException e) when (e.WriteErrors.Any(w => w.Category == ServerErrorCategory.DuplicateKey))
        {
            throw new ConcurrencyException(stream, expectedVersion);
        }
    }

    public async Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(string stream, CancellationToken ct = default) =>
        ToStoredEvents(await _events.Find(e => e.Stream == stream).SortBy(e => e.Version).ToListAsync(ct));

    public async Task<IReadOnlyList<StoredEvent>> ReadAllAsync(CancellationToken ct = default) =>
        ToStoredEvents(await _events.Find(FilterDefinition<EventDocument>.Empty).SortBy(e => e.Id).ToListAsync(ct));

    private static List<StoredEvent> ToStoredEvents(IEnumerable<EventDocument> documents) =>
        documents
            .Select(d => new StoredEvent(
                d.Stream,
                d.Version,
                EventSerializer.Deserialize(d.Type, d.Data),
                new DateTimeOffset(DateTime.SpecifyKind(d.Timestamp, DateTimeKind.Utc))))
            .ToList();
}
