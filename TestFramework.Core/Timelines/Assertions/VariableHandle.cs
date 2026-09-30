using System;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Wraps a variable lookup result so consumers can assert on existence or project its value.
/// </summary>
/// <typeparam name="T">The variable value type.</typeparam>
public class VariableHandle<T>
{
    private readonly T? _value;
    private readonly bool _exists;
    private readonly string _name;
    private readonly ScopedLogger? _logger;
    private readonly Func<Exception> _missing;

    internal VariableHandle(T? value, bool exists, string name, ScopedLogger? logger, Func<Exception> missing)
    {
        _value = value;
        _exists = exists;
        _name = name;
        _logger = logger;
        _missing = missing;
    }

    /// <summary>
    /// Starts an assertion chain for the resolved variable.
    /// </summary>
    public VariableAsserter<T> Should() => new VariableAsserter<T>(_value, _exists, _name, _logger);

    /// <summary>
    /// Projects the resolved variable value to another value while preserving the logging context.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="selector">Maps the resolved variable value to a new value.</param>
    /// <exception cref="Exceptions.MissingVariableException">The variable was never set, so there is nothing to project.</exception>
    public ValueHandle<TNew> Select<TNew>(Func<T, TNew> selector)
        => _exists
            ? new ValueHandle<TNew>(selector(_value!), _name, _logger)
            : throw _missing();
}
