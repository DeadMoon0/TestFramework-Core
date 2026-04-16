using System;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Timelines.Assertions;

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

    public ValueAsserter<T> Should() => new ValueAsserter<T>(_value, _expression, _logger);

    public ValueHandle<TNew> Select<TNew>(Func<T, TNew> selector)
        => new ValueHandle<TNew>(selector(_value), _expression, _logger);
}
