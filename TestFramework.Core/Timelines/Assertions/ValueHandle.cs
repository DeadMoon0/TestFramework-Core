using System;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Wraps a value so consumers can project it or start an assertion chain.
/// </summary>
/// <typeparam name="T">The wrapped value type.</typeparam>
public class ValueHandle<T>
{
    private readonly T _value;
    private readonly string _expression;
    private readonly ScopedLogger? _logger;

    internal ValueHandle(T value, string expression, ScopedLogger? logger)
    {
        _value = value;
        // Ensure the expression is presented consistently in logs (wrapped in single quotes)
        _expression = string.IsNullOrEmpty(expression) ? expression : (expression.StartsWith("'") ? expression : $"'{expression}'");
        _logger = logger;
    }

    /// <summary>
    /// Starts an assertion chain for the wrapped value.
    /// </summary>
    public ValueAsserter<T> Should() => new ValueAsserter<T>(_value, _expression, _logger);

    /// <summary>
    /// Projects the wrapped value to another value while preserving the logging context.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="selector">Maps the wrapped value to a new value.</param>
    public ValueHandle<TNew> Select<TNew>(Func<T, TNew> selector)
        => new ValueHandle<TNew>(selector(_value), _expression, _logger);
}
