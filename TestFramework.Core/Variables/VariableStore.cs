using Newtonsoft.Json;
using System.Collections.Generic;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Variables;

/// <summary>
/// Stores resolved runtime variables for a timeline run and reports changes to logging and debugging surfaces.
/// </summary>
public class VariableStore : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the variable store has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the variable store against further mutation.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private readonly FreezableDictionary<VariableIdentifier, object?> _variables = [];
    private readonly ScopedLogger logger;
    private readonly DebuggingRunSession debuggingSession;

    internal VariableStore(ScopedLogger logger, DebuggingRunSession debuggingSession)
    {
        this.logger = logger;
        this.debuggingSession = debuggingSession;
    }

    /// <summary>
    /// Sets or replaces a variable value in the store.
    /// </summary>
    /// <typeparam name="T">The variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to set.</param>
    /// <param name="value">The value to store.</param>
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

    /// <summary>
    /// Gets a variable value without a static result type.
    /// </summary>
    /// <param name="identifier">The variable identifier to resolve.</param>
    public object? GetVariable(VariableIdentifier identifier)
    {
        return _variables[identifier];
    }

    /// <summary>
    /// Gets a variable value as a typed value.
    /// </summary>
    /// <typeparam name="T">The expected variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to resolve.</param>
    public T? GetVariable<T>(VariableIdentifier identifier)
    {
        return (T?)_variables[identifier];
    }

    /// <summary>
    /// Attempts to get a typed variable value.
    /// </summary>
    /// <typeparam name="T">The expected variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to resolve.</param>
    /// <param name="value">The resolved value when present.</param>
    /// <returns><see langword="true"/> when the variable exists; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Returns all currently stored variables.
    /// </summary>
    public IEnumerable<KeyValuePair<VariableIdentifier, object?>> GetAll()
    {
        return _variables;
    }
}