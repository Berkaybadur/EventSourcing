using MongoDB.Driver;
using TaskBoard.Api.Infrastructure;
using TaskBoard.Domain.Events;

namespace TaskBoard.Tests;

/// <summary>Behaviour every <see cref="IEventStore"/> implementation must satisfy.</summary>
public abstract class EventStoreContract
{
    protected abstract IEventStore CreateStore();

    private static object[] Events(Guid id) =>
        [new TaskCreated(id, "A", "ann"), new TaskAssigned(id, "bob", "ann")];

    [TestMethod]
    public async Task Appended_events_are_read_back_in_order_with_versions()
    {
        var store = CreateStore();
        var id = Guid.NewGuid();

        await store.AppendAsync("s-1", -1, Events(id));
        var stored = await store.ReadStreamAsync("s-1");

        Assert.HasCount(2, stored);
        CollectionAssert.AreEqual(new long[] { 0, 1 }, stored.Select(e => e.Version).ToArray());
        Assert.IsInstanceOfType<TaskCreated>(stored[0].Data);
        Assert.IsInstanceOfType<TaskAssigned>(stored[1].Data);
    }

    [TestMethod]
    public async Task Appending_with_a_stale_expected_version_is_rejected()
    {
        var store = CreateStore();
        var id = Guid.NewGuid();
        await store.AppendAsync("s-1", -1, Events(id));

        await Assert.ThrowsAsync<ConcurrencyException>(() => store.AppendAsync("s-1", -1, Events(id)));
        Assert.HasCount(2, await store.ReadStreamAsync("s-1"));
    }

    [TestMethod]
    public async Task Streams_are_independent_and_ReadAll_returns_everything()
    {
        var store = CreateStore();
        await store.AppendAsync("s-1", -1, Events(Guid.NewGuid()));
        await store.AppendAsync("s-2", -1, Events(Guid.NewGuid()));

        Assert.HasCount(2, await store.ReadStreamAsync("s-2"));
        Assert.HasCount(4, await store.ReadAllAsync());
    }
}

[TestClass]
public class InMemoryEventStoreTests : EventStoreContract
{
    protected override IEventStore CreateStore() => new InMemoryEventStore();
}

/// <summary>Runs the same contract against MongoDB when TASKBOARD_TEST_MONGO is set (e.g. mongodb://localhost:27017).</summary>
[TestClass]
public class MongoEventStoreTests : EventStoreContract
{
    protected override IEventStore CreateStore()
    {
        var connectionString = Environment.GetEnvironmentVariable("TASKBOARD_TEST_MONGO");
        if (string.IsNullOrEmpty(connectionString)) Assert.Inconclusive("Set TASKBOARD_TEST_MONGO to run MongoDB tests.");

        var database = new MongoClient(connectionString).GetDatabase($"taskboard_tests_{Guid.NewGuid():N}");
        return new MongoEventStore(database);
    }
}
