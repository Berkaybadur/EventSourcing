using TaskBoard.Domain;
using TaskBoard.Domain.Events;

namespace TaskBoard.Tests;

[TestClass]
public class TaskItemTests
{
    private static TaskItem NewTask()
    {
        var task = new TaskItem();
        task.Create(Guid.NewGuid(), "Write docs", "ann");
        return task;
    }

    [TestMethod]
    public void Create_raises_event_and_sets_state()
    {
        var task = NewTask();

        Assert.AreEqual("Write docs", task.Title);
        Assert.AreEqual(BoardSection.Open, task.Section);
        Assert.IsInstanceOfType<TaskCreated>(task.GetChanges().Single());
        Assert.AreEqual(-1, task.Version, "version only advances once events are persisted");
    }

    [TestMethod]
    public void Create_twice_is_rejected() =>
        Assert.Throws<TaskAlreadyCreatedException>(() => NewTask().Create(Guid.NewGuid(), "x", "ann"));

    [TestMethod]
    public void Blank_title_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new TaskItem().Create(Guid.NewGuid(), " ", "ann"));

    [TestMethod]
    public void Assign_and_move_update_state()
    {
        var task = NewTask();

        task.Assign("bob", "ann");
        task.Move(BoardSection.InProgress, "bob");

        Assert.AreEqual("bob", task.AssignedTo);
        Assert.AreEqual(BoardSection.InProgress, task.Section);
        Assert.HasCount(3, task.GetChanges());
    }

    [TestMethod]
    public void Completed_task_rejects_further_changes()
    {
        var task = NewTask();
        task.Complete("ann");

        Assert.IsTrue(task.IsCompleted);
        Assert.AreEqual(BoardSection.Done, task.Section);
        Assert.Throws<TaskCompletedException>(() => task.Assign("bob", "ann"));
        Assert.Throws<TaskCompletedException>(() => task.Move(BoardSection.Open, "ann"));
        Assert.Throws<TaskCompletedException>(() => task.Complete("ann"));
    }

    [TestMethod]
    public void Commands_on_a_task_that_does_not_exist_are_rejected() =>
        Assert.Throws<TaskNotFoundException>(() => new TaskItem().Assign("bob", "ann"));

    [TestMethod]
    public void Rehydrating_from_history_restores_state_and_version()
    {
        var id = Guid.NewGuid();
        object[] history =
        [
            new TaskCreated(id, "Ship it", "ann"),
            new TaskAssigned(id, "bob", "ann"),
            new TaskMoved(id, BoardSection.InProgress, "bob")
        ];

        var task = new TaskItem();
        task.LoadFromHistory(history);

        Assert.AreEqual(id, task.Id);
        Assert.AreEqual("bob", task.AssignedTo);
        Assert.AreEqual(BoardSection.InProgress, task.Section);
        Assert.AreEqual(2, task.Version);
        Assert.IsEmpty(task.GetChanges());
    }

    [TestMethod]
    public void AcceptChanges_advances_version_and_clears_pending_events()
    {
        var task = NewTask();
        task.Assign("bob", "ann");

        task.AcceptChanges();

        Assert.AreEqual(1, task.Version);
        Assert.IsEmpty(task.GetChanges());
    }
}
