namespace AStarDev.FunctionalParadigm;

/// <summary>Operators on <see cref="Option{T}" />.</summary>
public static class OptionExtensions
{
    extension<T>(Option<T> option)
    {
        /// <summary>Collapses the option to one value: <c>onSome</c> runs for a <see cref="Some{T}" />, <c>onNone</c> for a <see cref="None{T}" />. Exactly one runs.</summary>
        /// <example>
        /// <code>
        /// var label = Option.Some(3).Match(count => $"{count} items", () => "empty");
        /// </code>
        /// </example>
        public TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone)
            => option switch
            {
                Some<T> some => onSome(some.Value),
                None<T> => onNone()
            };

        /// <summary>Transforms a present value and leaves an absent one untouched. An exception thrown by <paramref name="selector" /> propagates.</summary>
        /// <example>
        /// <code>
        /// var length = Option.Some("abc").Map(text => text.Length);
        /// </code>
        /// </example>
        public Option<TResult> Map<TResult>(Func<T, TResult> selector)
            => option switch
            {
                Some<T> some => new Some<TResult>(selector(some.Value)),
                None<T> => new None<TResult>()
            };

        /// <summary>Chains a step that may itself be absent; the step runs only for a <see cref="Some{T}" />. An exception thrown by <paramref name="binder" /> propagates.</summary>
        /// <example>
        /// <code>
        /// var parent = Option.Some(path).Bind(FindParent);
        /// </code>
        /// </example>
        public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder)
            => option switch
            {
                Some<T> some => binder(some.Value),
                None<T> => new None<TResult>()
            };

        /// <summary>Returns the present value, or <paramref name="fallback" /> when absent.</summary>
        /// <param name="fallback">Used only for a <see cref="None{T}" />. Evaluated eagerly; it is not a lazy default.</param>
        public T GetValueOrDefault(T fallback) => option.Match(value => value, () => fallback);
    }
}
