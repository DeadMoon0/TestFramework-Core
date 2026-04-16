using Newtonsoft.Json;
using System.Collections.Generic;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Variables;

public class VariableStore : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private readonly FreezableDictionary<VariableIdentifier, object?> _variables = [];
    private readonly ScopedLogger logger;
    private readonly DebuggingRunSession debuggingSession;

    internal VariableStore(ScopedLogger logger, DebuggingRunSession debuggingSession)
    {
        this.logger = logger;
        this.debuggingSession = debuggingSession;
    }

    public void SetVariable<T>(VariableIdentifier identifier, T value)
    {
        var newValue = Logging.VariableFormatter.Format(value);
        if (_variables.TryGetValue(identifier, out var previousValue))
        {
            var oldValue = Logging.VariableFormatter.Format(previousValue);
            if (oldValue == newValue)
            {
                logger.LogInformation("Set Variable ({0}) = {1} (unchanged)", identifier, newValue);
                _variables[identifier] = value;
                return;
            }

            logger.LogInformation(
                "Set Variable ({0}) {1} -> {2}",
                identifier,
                oldValue,
                newValue);
        }
        else
        {
            logger.LogInformation("Set Variable ({0}) = {1}", identifier, newValue);
        }

        _variables[identifier] = value;
        debuggingSession.UpdateVariableAsync(identifier, GetDebuggingStateFromValue(value, identifier));
    }

    internal static VariableState GetDebuggingStateFromValue(object? value, VariableIdentifier identifier)
    {
        return new VariableState { Key = identifier, TypeName = value?.GetType().FullName ?? "", Value = JsonConvert.SerializeObject(value) };
    }

    public object? GetVariable(VariableIdentifier identifier)
    {
        return _variables[identifier];
    }

    public T? GetVariable<T>(VariableIdentifier identifier)
    {
        return (T?)_variables[identifier];
    }

    public bool TryGetVariable<T>(VariableIdentifier identifier, out T? value)
    {
        if (_variables.TryGetValue(identifier, out object? raw))
        {
            value = (T?)raw;
            return true;
        }
        value = default;
        return false;
    }

    public IEnumerable<KeyValuePair<VariableIdentifier, object?>> GetAll()
    {
        return _variables;
    }
}