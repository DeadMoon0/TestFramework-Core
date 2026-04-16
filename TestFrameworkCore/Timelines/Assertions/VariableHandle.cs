using System;
using TestFrameworkCore.Logging;

namespace TestFrameworkCore.Timelines.Assertions;

public class VariableHandle<T>
{
    private readonly T? _value;
    private readonly bool _exists;
    private readonly string _name;
    private readonly ScopedLogger? _logger;

    internal VariableHandle(T? value, bool exists, string name, ScopedLogger? logger)
    {
        _value = value;
        _exists = exists;
        _name = name;
        _logger = logger;
    }

    public VariableAsserter<T> Should() => new VariableAsserter<T>(_value, _exists, _name, _logger);

    public ValueHandle<TNew> Select<TNew>(Func<T, TNew> selector)
        => new ValueHandle<TNew>(selector(_value!), _name, _logger);
}
