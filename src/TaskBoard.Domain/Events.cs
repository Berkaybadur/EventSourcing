namespace TaskBoard.Domain.Events;

public enum BoardSection
{
    Open = 1,
    InProgress = 2,
    Done = 3
}

// Events are immutable facts, named in the past tense. They are persisted by type name,
// so renaming one is a breaking change to stored streams.
public sealed record TaskCreated(Guid TaskId, string Title, string CreatedBy);

public sealed record TaskAssigned(Guid TaskId, string AssignedTo, string AssignedBy);

public sealed record TaskMoved(Guid TaskId, BoardSection Section, string MovedBy);

public sealed record TaskCompleted(Guid TaskId, string CompletedBy);
