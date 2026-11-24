using TaskBoard.Domain.Events;

namespace TaskBoard.Domain;

/// <summary>A task on a kanban board, modelled as an event-sourced aggregate.</summary>
public sealed class TaskItem : Aggregate
{
    public string Title { get; private set; } = string.Empty;
    public BoardSection Section { get; private set; }
    public string? AssignedTo { get; private set; }
    public bool IsCompleted { get; private set; }

    public void Create(Guid taskId, string title, string createdBy)
    {
        if (Version >= 0 || Id != Guid.Empty) throw new TaskAlreadyCreatedException();
        if (taskId == Guid.Empty) throw new ArgumentException("Task id is required.", nameof(taskId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        Raise(new TaskCreated(taskId, title.Trim(), createdBy));
    }

    public void Assign(string assignedTo, string assignedBy)
    {
        EnsureModifiable();
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedTo);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedBy);

        Raise(new TaskAssigned(Id, assignedTo, assignedBy));
    }

    public void Move(BoardSection section, string movedBy)
    {
        EnsureModifiable();
        if (!Enum.IsDefined(section)) throw new ArgumentOutOfRangeException(nameof(section));
        ArgumentException.ThrowIfNullOrWhiteSpace(movedBy);

        Raise(new TaskMoved(Id, section, movedBy));
    }

    public void Complete(string completedBy)
    {
        EnsureModifiable();
        ArgumentException.ThrowIfNullOrWhiteSpace(completedBy);

        Raise(new TaskCompleted(Id, completedBy));
    }

    private void EnsureModifiable()
    {
        if (Id == Guid.Empty) throw new TaskNotFoundException();
        if (IsCompleted) throw new TaskCompletedException();
    }

    protected override void When(object @event)
    {
        switch (@event)
        {
            case TaskCreated e:
                Id = e.TaskId;
                Title = e.Title;
                Section = BoardSection.Open;
                break;
            case TaskAssigned e:
                AssignedTo = e.AssignedTo;
                break;
            case TaskMoved e:
                Section = e.Section;
                break;
            case TaskCompleted:
                IsCompleted = true;
                Section = BoardSection.Done;
                break;
        }
    }
}
