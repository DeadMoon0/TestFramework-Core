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
    public void Freeze() { lock (syncRoot) { IsFrozen = true; _variables.Freeze(); } }

    private readonly FreezableDictionary<VariableIdentifier, object?> _variables = [];
    private readonly ScopedLogger logger;
    private readonly DebuggingRunSession debuggingSession;

    /// <summary>
    /// Last published content fingerprint per variable. Only populated while something is capturing.
    /// </summary>
    private readonly Dictionary<VariableIdentifier, string> changeTokens = [];
    private readonly object changeTokenLock = new();

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
        // Hold the lock only for the dictionary read and write. Formatting a value can be arbitrarily
        // expensive, and it used to happen up to three times per write with the lock held.
        lock (syncRoot)
        {
            ((IFreezable)this).EnsureNotFrozen();
            _variables[identifier] = value;
        }

        if (!debuggingSession.IsCapturing)
            return;

        // Described and fingerprinted in one pass. Content fingerprint, not display text: the display
        // form is truncated, so using it as the change rule silently dropped updates whose values
        // differed only past the cut-off.
        DescribedValue described = DebugValueDescriber.Describe(value);

        lock (changeTokenLock)
        {
            if (changeTokens.TryGetValue(identifier, out string? previousToken) && previousToken == described.ChangeToken)
                return;

            changeTokens[identifier] = described.ChangeToken;
        }

        debuggingSession.PublishVariableUpdate(
            identifier,
            GetDebuggingStateFromValue(value, identifier, described with { Description = WithBody(described, identifier) }));
    }

    /// <summary>
    /// Writes the value out and points the description at it, when the preview could not carry it.
    /// </summary>
    /// <remarks>
    /// The condition is the preview having been cut, rather than a size threshold of its own. Those
    /// are the same question asked twice, and two answers that can disagree would leave a consumer
    /// showing a truncated value with no way to reach the rest of it.
    /// </remarks>
    private DebugValueDescription WithBody(DescribedValue described, VariableIdentifier identifier)
    {
        if (described.Description.Preview?.IsTruncated != true || described.Content is null)
            return described.Description;

        return described.Description with
        {
            Body = debuggingSession.ValueFiles.Write(identifier.Identifier, described.Content)
        };
    }

    internal static DebugValue GetDebuggingStateFromValue(object? value, VariableIdentifier identifier, string? displayText = null)
        => GetDebuggingStateFromValue(value, identifier, DebugValueDescriber.Describe(value), displayText);

    private static DebugValue GetDebuggingStateFromValue(object? value, VariableIdentifier identifier, DescribedValue described, string? displayText = null)
    {
        string typeName = value?.GetType().FullName ?? "null";

        return new DebugValue
        {
            Key = identifier,
            Envelope = new DebugValueEnvelope
            {
                Kind = DebugValueKind.Variable,
                TypeName = typeName,
                Description = described.Description,

                // Keyed by shape, not by CLR type. The type is already on the envelope for anyone
                // who wants it; what it could never be was a key, because registering a renderer
                // against it means naming every concrete type a run might assign.
                SchemaKey = DebugValueSchemaKeys.Of(described.Description.Shape),

                // No second copy of the value here. This used to carry the whole thing serialised as JSON,
                // beside a preview of it and a one-line rendering of it - three passes over the same object
                // on every write, of which consumers read one. The description states what it is, its preview
                // carries it when it fits, and a value too big for a preview is written to a file.
            }
        };
    }

    private static JToken ToToken(object? value)
    {
        if (value is null)
            return JValue.CreateNull();

        try
        {
            return JToken.FromObject(value, JsonSerializer.CreateDefault());
        }
        catch (JsonException)
        {
            // A value that cannot be serialized is still worth reporting; losing the whole run to a
            // debug-payload failure is not an acceptable trade.
            return new JValue($"<unserializable {value.GetType().FullName}>");
        }
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