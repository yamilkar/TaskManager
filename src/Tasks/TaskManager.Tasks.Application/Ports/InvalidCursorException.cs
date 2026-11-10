namespace TaskManager.Tasks.Application.Ports;

/// <summary>Thrown by a repository when a paging cursor is malformed or does not belong to the caller.</summary>
public sealed class InvalidCursorException(string message) : Exception(message);
