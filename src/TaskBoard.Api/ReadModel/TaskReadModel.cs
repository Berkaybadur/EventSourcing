using System.Collections.Concurrent;
using TaskBoard.Api.Infrastructure;
using TaskBoard.Domain.Events;

namespace TaskBoard.Api.ReadModel;

/// <summary>Query-side representation of a task (the "Q" in CQRS).</summary>
public sealed record TaskView(Guid Id, string Title, BoardSection Section, string? AssignedTo, bool IsCompleted);

/// <summary>In-memory projection of the task events, kept current as events are saved.</summary>
public sealed class TaskReadModel : IProjection
{
    private readonly ConcurrentDictionary<Guid, TaskView> _tasks = new();

    public TaskView? Get(Guid id) => _tasks.GetValueOrDefault(id);

    public IReadOnlyList<TaskView> List() => _tasks.Values.OrderBy(t => t.Title).ToList();

    public void Apply(object @event)
    {
        switch (@event)
        {
            case TaskCreated e:
                _tasks[e.TaskId] = new TaskView(e.TaskId, e.Title, BoardSection.Open, null, false);
                break;
            case TaskAssigned e:
                _tasks.AddOrUpdate(e.TaskId, _ => throw new InvalidOperationException(), (_, t) => t with { AssignedTo = e.AssignedTo });
                break;
            case TaskMoved e:
                _tasks.AddOrUpdate(e.TaskId, _ => throw new InvalidOperationException(), (_, t) => t with { Section = e.Section });
                break;
            case TaskCompleted e:
                _tasks.AddOrUpdate(e.TaskId, _ => throw new InvalidOperationException(),
                    (_, t) => t with { IsCompleted = true, Section = BoardSection.Done });
                break;
        }
    }
}

/// <summary>Rebuilds the read model from the full event history when the app starts.</summary>
public sealed class ProjectionRebuilder(IEventStore store, TaskReadModel readModel) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var stored in await store.ReadAllAsync(ct)) readModel.Apply(stored.Data);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
