using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace AStarDev.FunctionalParadigm;

/// <summary>
/// Pipeline operators for <see cref="Exceptional{T}" />, <c>Task&lt;Exceptional&lt;T&gt;&gt;</c> and <c>ValueTask&lt;Exceptional&lt;T&gt;&gt;</c>.
/// </summary>
/// <remarks>
/// <see cref="Exceptional{T}" /> carries an exception that was already captured, usually by <see cref="Try" />.
/// The operators never capture exceptions themselves: only <see cref="Try.Run{T}" /> and <see cref="Try.RunAsync{T}" /> do.
/// A pipeline therefore reads as: capture with <see cref="Try" />, shape with <c>Map</c>, <c>Bind</c> and <c>Tap</c>,
/// then leave with <c>Match</c> or <c>ToResult</c>.
/// </remarks>
/// <example>
/// <code>
/// Result&lt;int, string&gt; result = await Try.RunAsync(token => LoadAsync(path, token), cancellationToken)
///     .MapAsync(text => text.Length)
///     .TapErrorAsync(exception => logger.LogError(exception, "load failed"))
///     .ToResultAsync(exception => exception.Message);
/// </code>
/// </example>
public static class ExceptionalExtensions
{
    private const string UnexpectedExceptionalTypeMessage = "Unexpected exceptional type.";

    [DoesNotReturn]
    private static TOut Rethrow<TOut>(Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();

        throw exception;
    }

    extension<T>(Exceptional<T> exceptional)
    {
        /// <summary>
        /// Collapses the outcome to one value: <c>onSuccess</c> runs for a <see cref="Success{T}"/>, <c>onFailure</c> for a <see cref="Failure{T}"/>. Exactly one runs.
        /// </summary>
        /// <remarks>
        /// Use this at the edge of a pipeline, where the outcome has to become a plain value such as a message or status code.
        /// </remarks>
        /// <example>
        /// <code>
        /// var message = Try.Run(() =&gt; int.Parse(text))
        ///     .Match(number =&gt; $"parsed {number}", exception =&gt; $"rejected: {exception.Message}");
        /// </code>
        /// </example>
        public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Exception, TOut> onFailure)
            => exceptional switch
            {
                Success<T> success => onSuccess(success.Value),
                Failure<T> failure => onFailure(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage + $" Type: {exceptional.GetType().FullName}")
            };

        /// <summary>
        /// Returns the value, or rethrows the captured exception with its original stack trace preserved.
        /// </summary>
        /// <remarks>
        /// Use only where an exception is the right outcome, such as at a framework boundary. Inside the application prefer <c>Match</c> or <c>ToResult</c>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var number = Try.Run(() =&gt; int.Parse("42")).GetOrThrow();
        /// </code>
        /// </example>
        public T GetOrThrow()            => exceptional.Match(value => value, Rethrow<T>);

        /// <summary>
        /// <c>Match</c> where only the success handler is asynchronous. The failure handler stays synchronous.
        /// </summary>
        /// <example>
        /// <code>
        /// var body = await Try.Run(() =&gt; path).MatchAsync(
        ///     async file =&gt; await File.ReadAllTextAsync(file),
        ///     exception =&gt; exception.Message);
        /// </code>
        /// </example>
        public async Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> onSuccess, Func<Exception, TOut> onFailure)
            => exceptional switch
            {
                Success<T> success => await onSuccess(success.Value).ConfigureAwait(false),
                Failure<T> failure => onFailure(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };

        /// <summary>
        /// Transforms the success value and leaves a failure untouched, so a chain of maps skips every step after the first failure.
        /// </summary>
        /// <remarks>
        /// An exception thrown by the delegate is not captured: it propagates to the caller. Wrap the work in <see cref="Try.Run{T}"/> when it can throw and the throw should become a <see cref="Failure{T}"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var length = Try.Run(() =&gt; File.ReadAllText(path)).Map(text =&gt; text.Length);
        /// </code>
        /// </example>
        public Exceptional<TResult> Map<TResult>(Func<T, TResult> selector)
            => exceptional switch
            {
                Success<T> success => new Success<TResult>(selector(success.Value)),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };

        /// <summary>
        /// <c>Map</c> with a <see cref="Task{TResult}"/>-returning selector. The selector is awaited only for a <see cref="Success{T}"/>.
        /// </summary>
        /// <remarks>
        /// An exception thrown by the delegate is not captured: it propagates to the caller. Wrap the work in <see cref="Try.Run{T}"/> when it can throw and the throw should become a <see cref="Failure{T}"/>.
        /// </remarks>
        public async Task<Exceptional<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> selector)
            => exceptional switch
            {
                Success<T> success => new Success<TResult>(await selector(success.Value).ConfigureAwait(false)),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };

        /// <summary>
        /// <c>Map</c> with a <see cref="ValueTask{TResult}"/>-returning selector. The selector is awaited only for a <see cref="Success{T}"/>.
        /// </summary>
        /// <remarks>
        /// An exception thrown by the delegate is not captured: it propagates to the caller. Wrap the work in <see cref="Try.Run{T}"/> when it can throw and the throw should become a <see cref="Failure{T}"/>.
        /// </remarks>
        public async ValueTask<Exceptional<TResult>> MapAsync<TResult>(Func<T, ValueTask<TResult>> selector)
            => exceptional switch
            {
                Success<T> success => new Success<TResult>(await selector(success.Value).ConfigureAwait(false)),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };

        /// <summary>
        /// Chains a step that can itself fail. A failure at any step short-circuits: later binders never run and the first failure is returned.
        /// </summary>
        /// <remarks>
        /// Use <c>Bind</c> when the next step returns an <see cref="Exceptional{T}"/>, and <c>Map</c> when it returns a plain value. An exception thrown by the delegate is not captured: it propagates to the caller. Wrap the work in <see cref="Try.Run{T}"/> when it can throw and the throw should become a <see cref="Failure{T}"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// Exceptional&lt;int&gt; Parse(string text) =&gt; Try.Run(() =&gt; int.Parse(text));
        /// Exceptional&lt;int&gt; Halve(int number) =&gt; number % 2 == 0
        ///     ? Exceptional.Success(number / 2)
        ///     : Exceptional.Failure&lt;int&gt;(new InvalidOperationException("odd"));
        /// 
        /// var result = Parse("84").Bind(Halve);
        /// </code>
        /// </example>
        public Exceptional<TResult> Bind<TResult>(Func<T, Exceptional<TResult>> binder)
            => exceptional switch
            {
                Success<T> success => binder(success.Value),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };

        /// <summary>
        /// <c>Bind</c> with a <see cref="Task{TResult}"/>-returning binder. The binder is awaited only for a <see cref="Success{T}"/>.
        /// </summary>
        public async Task<Exceptional<TResult>> BindAsync<TResult>(Func<T, Task<Exceptional<TResult>>> binder)
            => exceptional switch
            {
                Success<T> success => await binder(success.Value).ConfigureAwait(false),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };

        /// <summary>
        /// Runs a side effect for the case present, then returns the original outcome unchanged. Use it for logging or metrics that must not alter the pipeline.
        /// </summary>
        /// <remarks>
        /// <c>onFailure</c> is optional. An exception thrown by either action propagates; it is not captured.
        /// </remarks>
        /// <example>
        /// <code>
        /// var outcome = Try.Run(() =&gt; Load())
        ///     .Tap(value =&gt; logger.LogInformation("loaded {Value}", value),
        ///          exception =&gt; logger.LogWarning(exception, "load failed"));
        /// </code>
        /// </example>
        public Exceptional<T> Tap(Action<T> onSuccess, Action<Exception>? onFailure = null)
        {
            switch (exceptional)
            {
                case Success<T> success:
                    onSuccess(success.Value);

                    return success;

                case Failure<T> failure:
                    onFailure?.Invoke(failure.Exception);

                    return failure;

                default:
                    throw new InvalidOperationException(UnexpectedExceptionalTypeMessage);
            }
        }

        /// <summary>
        /// Runs a side effect only when the outcome is a <see cref="Failure{T}"/>, then returns the original outcome unchanged.
        /// </summary>
        /// <remarks>
        /// Shorthand for <c>Tap</c> with no success action.
        /// </remarks>
        public Exceptional<T> TapError(Action<Exception> onFailure)
        {
            if (exceptional is Failure<T> failure) onFailure(failure.Exception);

            return exceptional;
        }

        /// <summary>
        /// Leaves the exception world: maps a captured exception to a domain error and returns a <see cref="Result{TResult,TError}"/>.
        /// </summary>
        /// <remarks>
        /// Use at a module boundary so callers see an error value they can match exhaustively, not an <see cref="Exception"/>. <c>mapError</c> runs only for a <see cref="Failure{T}"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// Result&lt;int, string&gt; result = Try.Run(() =&gt; int.Parse(text))
        ///     .ToResult(exception =&gt; exception.Message);
        /// </code>
        /// </example>
        public Result<T, TError> ToResult<TError>(Func<Exception, TError> mapError)
            => exceptional switch
            {
                Success<T> success => new Ok<T>(success.Value),
                Failure<T> failure => new Fail<TError>(mapError(failure.Exception)),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };
    }

    extension<T>(Task<Exceptional<T>> exceptionalTask)
    {
        /// <summary>
        /// <c>Match</c> on a task. Awaits the task first, so an exception thrown by the task itself propagates rather than reaching <c>onFailure</c>.
        /// </summary>
        public async Task<TOut> MatchAsync<TOut>(Func<T, TOut> onSuccess, Func<Exception, TOut> onFailure)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional.Match(onSuccess, onFailure);
        }

        /// <summary>
        /// Awaits the task, then runs <c>onSuccess</c> or <c>onFailure</c> for their side effects only.
        /// </summary>
        /// <remarks>
        /// Awaiting the task can still throw: only a <see cref="Failure{T}"/> value reaches <c>onFailure</c>.
        /// </remarks>
        public async Task MatchAsync(Func<T, Task> onSuccess, Action<Exception> onFailure)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            switch (exceptional)
            {
                case Success<T> success:
                    await onSuccess(success.Value).ConfigureAwait(false);

                    break;

                case Failure<T> failure:
                    onFailure(failure.Exception);

                    break;

                default:
                    throw new InvalidOperationException(UnexpectedExceptionalTypeMessage + $" Type: {exceptional.GetType().FullName}");
            }
        }

        /// <summary>
        /// <c>Map</c> on a task, so a pipeline can be written without awaiting each step. An exception thrown by the delegate is not captured: it propagates to the caller.
        /// </summary>
        public async Task<Exceptional<TResult>> MapAsync<TResult>(Func<T, TResult> selector)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional.Map(selector);
        }

        /// <summary>
        /// <c>Map</c> on a task, with an asynchronous selector. The selector is awaited only for a <see cref="Success{T}"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// var size = await Try.RunAsync(token =&gt; client.GetAsync(url, token), cancellationToken)
        ///     .MapAsync(async response =&gt; (await response.Content.ReadAsByteArrayAsync()).Length);
        /// </code>
        /// </example>
        public async Task<Exceptional<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> selector)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return await exceptional.MapAsync(selector).ConfigureAwait(false);
        }

        /// <summary>
        /// <c>Bind</c> on a task, with a <see cref="Task{TResult}"/>-returning binder. Short-circuits on a <see cref="Failure{T}"/>.
        /// </summary>
        public async Task<Exceptional<TResult>> BindAsync<TResult>(Func<T, Task<Exceptional<TResult>>> binder)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return await exceptional.BindAsync(binder).ConfigureAwait(false);
        }

        /// <summary>
        /// <c>Bind</c> on a task, with a <see cref="ValueTask{TResult}"/>-returning binder. Short-circuits on a <see cref="Failure{T}"/>.
        /// </summary>
        public async Task<Exceptional<TResult>> BindAsync<TResult>(Func<T, ValueTask<Exceptional<TResult>>> binder)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional switch
            {
                Success<T> success => await binder(success.Value).ConfigureAwait(false),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };
        }

        /// <summary>
        /// <c>Tap</c> on a task. The original outcome flows on unchanged.
        /// </summary>
        public async Task<Exceptional<T>> TapAsync(Action<T> onSuccess, Action<Exception>? onFailure = null)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional.Tap(onSuccess, onFailure);
        }

        /// <summary>
        /// <c>TapError</c> on a task. The original outcome flows on unchanged.
        /// </summary>
        public async Task<Exceptional<T>> TapErrorAsync(Action<Exception> onFailure)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional.TapError(onFailure);
        }

        /// <summary>
        /// Runs <c>finallyAction</c> once the task has produced an outcome, success or failure, and returns that outcome unchanged.
        /// </summary>
        /// <remarks>
        /// This is not a <c>finally</c> block: if the task itself throws, the action does not run and the exception propagates. Use it for cleanup that depends on the outcome having been produced.
        /// </remarks>
        /// <example>
        /// <code>
        /// var outcome = await Try.RunAsync(token =&gt; SendAsync(token), cancellationToken)
        ///     .EnsureAsync(() =&gt; gate.Release());
        /// </code>
        /// </example>
        public async Task<Exceptional<T>> EnsureAsync(Action finallyAction)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            finallyAction();

            return exceptional;
        }

        /// <summary>
        /// <c>ToResult</c> on a task. Awaits the task, then maps a captured exception to an error value.
        /// </summary>
        public async Task<Result<T, TError>> ToResultAsync<TError>(Func<Exception, TError> mapError)
            => (await exceptionalTask.ConfigureAwait(false)).ToResult(mapError);
    }

    extension<T>(ValueTask<Exceptional<T>> exceptionalTask)
    {
        /// <summary>
        /// <c>Bind</c> on a <see cref="ValueTask{TResult}"/>, with a <see cref="Task{TResult}"/>-returning binder.
        /// </summary>
        public async ValueTask<Exceptional<TResult>> BindAsync<TResult>(Func<T, Task<Exceptional<TResult>>> binder)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional switch
            {
                Success<T> success => await binder(success.Value).ConfigureAwait(false),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };
        }

        /// <summary>
        /// <c>Bind</c> on a <see cref="ValueTask{TResult}"/>, with a <see cref="ValueTask{TResult}"/>-returning binder.
        /// </summary>
        public async ValueTask<Exceptional<TResult>> BindAsync<TResult>(Func<T, ValueTask<Exceptional<TResult>>> binder)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional switch
            {
                Success<T> success => await binder(success.Value).ConfigureAwait(false),
                Failure<T> failure => new Failure<TResult>(failure.Exception),
                _ => throw new InvalidOperationException(UnexpectedExceptionalTypeMessage)
            };
        }

        /// <summary>
        /// <c>Tap</c> on a <see cref="ValueTask{TResult}"/>. The original outcome flows on unchanged.
        /// </summary>
        public async ValueTask<Exceptional<T>> TapAsync(Action<T> onSuccess, Action<Exception>? onFailure = null)
        {
            var exceptional = await exceptionalTask.ConfigureAwait(false);

            return exceptional.Tap(onSuccess, onFailure);
        }
    }
}
