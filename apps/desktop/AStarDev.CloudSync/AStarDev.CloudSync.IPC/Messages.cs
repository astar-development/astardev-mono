namespace AStarDev.CloudSync.IPC;

/// <summary>Requests a state change. Answered with a <see cref="CommandResult"/>, never with state.</summary>
public interface ICommand;

/// <summary>Reads state without mutating it. Answered with a projection view, never a domain model.</summary>
public interface IQuery<TResult>;

/// <summary>A fact published by the daemon to subscribers.</summary>
public interface IEvent;

/// <summary>How the daemon answered a command. Says whether the request was taken, not whether the work has finished.</summary>
public enum CommandOutcome
{
    /// <summary>The daemon took the request and will act on it.</summary>
    Accepted,

    /// <summary>The daemon understood the request and declined it, for example because of its current state. See the message.</summary>
    Rejected,

    /// <summary>The request itself was invalid, so it was not attempted. Fix the request before sending it again.</summary>
    ValidationError
}

/// <summary>The answer to an <see cref="ICommand"/>. Carries no state: ask with an <see cref="IQuery{TResult}"/> or subscribe to events to see the effect.</summary>
/// <param name="Outcome">Whether the command was accepted, rejected or invalid.</param>
/// <param name="Message">Why, when the outcome is not <see cref="CommandOutcome.Accepted"/>. Null when there is nothing to add.</param>
/// <param name="TraceId">The id of the daemon trace that handled a failed request, to quote when reporting it. Null when the outcome is accepted or the daemon was not tracing.</param>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account an accepted <see cref="ConnectAccount"/> signed in, also when the account was already known. Null for every other answer.</param>
public sealed record CommandResult(CommandOutcome Outcome, string? Message = null, string? TraceId = null, Guid? AccountId = null);
