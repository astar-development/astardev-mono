namespace AStarDev.CloudSync.IPC;

/// <summary>Requests a state change. Answered with a <see cref="CommandResult"/>, never with state.</summary>
public interface ICommand;

/// <summary>Reads state without mutating it. Answered with a projection view, never a domain model.</summary>
public interface IQuery<TResult>;

/// <summary>A fact published by the daemon to subscribers.</summary>
public interface IEvent;

public enum CommandOutcome
{
    Accepted,
    Rejected,
    ValidationError
}

public sealed record CommandResult(CommandOutcome Outcome, string? Message = null);
