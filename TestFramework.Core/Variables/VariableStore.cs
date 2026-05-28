using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Variables;

/// <summary>
/// Stores resolved runtime variables for a timeline run and reports changes to logging and debugging surfaces.
/// </summary>
public class VariableStore : IFreezable
{
    private readonly object syncRoot = new();

    /// <summary>
    /// Gets a value indicating whether the variable store has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the variable store against further mutation.
    /// </summary>
    public void Freeze() { lock (syncRoot) { IsFrozen = true; } }

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
        string newValue = Logging.VariableFormatter.Format(value);
        string? oldValue = null;
        bool existed;
        bool unchanged;

        lock (syncRoot)
        {
            existed = _variables.TryGetValue(identifier, out var previousValue);
            oldValue = existed ? Logging.VariableFormatter.Format(previousValue) : null;
            unchanged = existed && oldValue == newValue;
            _variables[identifier] = value;
        }

        if (unchanged)
        {
            return;
        }

        debuggingSession.PublishVariableUpdate(identifier, GetDebuggingStateFromValue(value, identifier));
    }

    internal static VariableState GetDebuggingStateFromValue(object? value, VariableIdentifier identifier)
    {
        string typeName = value?.GetType().FullName ?? "null";
        return new VariableState
        {
            Key = identifier,
            Envelope = new DebugValueEnvelope
            {
                Kind = DebugValueKind.Variable,
                TypeName = typeName,
                DisplayText = Logging.VariableFormatter.Format(value),
                SchemaKey = $"tf.variable:{typeName}",
                Core = new JObject
                {
                    ["key"] = identifier.Identifier,
                    ["value"] = ToToken(value)
                }
            }
        };
    }

    private static JToken ToToken(object? value)
    {
        return value is null ? JValue.CreateNull() : JToken.FromObject(value, JsonSerializer.CreateDefault());
    }

    /// <summary>
    /// Gets a variable value without a static result type.
    /// </summary>
    /// <param name="identifier">The variable identifier to resolve.</param>
    public object? GetVariable(VariableIdentifier identifier)
    {
        lock (syncRoot)
        {
            return _variables[identifier];
        }
    }

    /// <summary>
    /// Gets a variable value as a typed value.
    /// </summary>
    /// <typeparam name="T">The expected variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to resolve.</param>
    public T? GetVariable<T>(VariableIdentifier identifier)
    {
        lock (syncRoot)
        {
            return (T?)_variables[identifier];
        }
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
        lock (syncRoot)
        {
            if (_variables.TryGetValue(identifier, out object? raw))
            {
                value = (T?)raw;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns all currently stored variables.
    /// </summary>
    public IEnumerable<KeyValuePair<VariableIdentifier, object?>> GetAll()
    {
        lock (syncRoot)
        {
            return _variables.ToArray();
        }
    }
}