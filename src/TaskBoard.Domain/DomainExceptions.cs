namespace TaskBoard.Domain;

public abstract class DomainException(string message) : Exception(message);

public sealed class TaskNotFoundException() : DomainException("Task not found.");

public sealed class TaskAlreadyCreatedException() : DomainException("Task has already been created.");

public sealed class TaskCompletedException() : DomainException("Task is completed and can no longer be changed.");
