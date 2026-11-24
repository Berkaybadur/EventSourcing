# EventSourcing: TaskBoard

A small but complete **Event Sourcing + CQRS** reference in **C# / .NET 10**: a kanban task board where state is never stored directly. Every change is an immutable event appended to a stream, and current state is rebuilt by replaying events.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![MongoDB](https://img.shields.io/badge/MongoDB-event%20store-47A248?logo=mongodb&logoColor=white)
![Tests](https://img.shields.io/badge/tests-16%20passing%20%2B%203%20Mongo-brightgreen)

## Architecture

```
                 commands                            queries
 HTTP  ->  load aggregate (replay)          HTTP  ->  TaskReadModel (projection)
           execute -> new events                          ^
           append (optimistic concurrency)                | Apply(event)
                 |                                        |
                 v                                        |
            IEventStore  ----- saved events --------------+
       (MongoDB | in-memory)      rebuilt from full history on startup
```

| Project | Responsibility |
| --- | --- |
| `TaskBoard.Domain` | Pure domain, no dependencies: `Aggregate` base class, `TaskItem` aggregate, event records, domain exceptions |
| `TaskBoard.Api` | Minimal API (commands + queries), `IEventStore` with MongoDB and in-memory implementations, `AggregateRepository`, read-model projection |
| `TaskBoard.Tests` | MSTest: domain rules, event-store contract, end-to-end API tests |

### Design decisions

- **Aggregate = decisions + state transitions.** Commands validate invariants and `Raise` events; a single `When` method mutates state, used for both new events and replay, so rehydrated state can never drift from live behaviour.
- **Optimistic concurrency.** `AppendAsync(stream, expectedVersion, events)` fails with `ConcurrencyException` (HTTP 409) if the stream moved. In MongoDB this is enforced by a unique index on `(Stream, Version)`.
- **Safe event deserialization.** Event types are resolved from an allow-list of domain events, never from a type name stored in the data.
- **Separate read model.** Queries are served from a projection (`TaskReadModel`) that is updated as events are saved and rebuilt from the full event history on startup. The raw audit trail is available at `/tasks/{id}/history`.
- **One contract, two stores.** `EventStoreContract` runs against the in-memory store always and against MongoDB when `TASKBOARD_TEST_MONGO` is set.

## API

| Method | Path | Description |
| --- | --- | --- |
| POST | `/tasks` | Create a task `{title, createdBy}` |
| POST | `/tasks/{id}/assign` | `{assignedTo, assignedBy}` |
| POST | `/tasks/{id}/move` | `{section: Open \| InProgress \| Done, movedBy}` |
| POST | `/tasks/{id}/complete` | `{completedBy}`; completed tasks reject further changes (409) |
| GET | `/tasks`, `/tasks/{id}` | Read model |
| GET | `/tasks/{id}/history` | Event stream of the task |

Interactive docs at `/scalar` in Development.

## Run

```bash
docker compose up --build            # API on http://localhost:8080 with MongoDB

# or locally
docker run -d -p 27017:27017 mongo:7
dotnet run --project src/TaskBoard.Api
```

Set `EventStore__Provider=InMemory` to run without a database.

## Test

```bash
dotnet test                                              # in-memory store + API tests
TASKBOARD_TEST_MONGO=mongodb://localhost:27017 dotnet test   # also runs the MongoDB store tests
```

## History

This repository started in 2023 as a .NET Core 3.1 sample on EventStoreDB and Couchbase. It was rewritten for .NET 10 with a MongoDB event store, a real read model and tests; the original is available in the git history.
