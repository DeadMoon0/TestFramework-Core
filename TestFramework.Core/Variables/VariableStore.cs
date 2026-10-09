using System.Collections.Generic;
using System.Linq;
using TestFramework.Core;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Runner;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Variables;

/// <summary>
/// Stores resolved runtime variables for a timeline run and reports changes to logging and debugging surfaces.
/// </summary>
public class VariableStore
{
    private readonly object syncRoot;

    private readonly Dictionary<VariableIdentifier, object?> _variables;

    /// <summary>
    /// The variables written as secrets. Shared with every attempt's view, like the values.
    /// </summary>
    private readonly HashSet<VariableIdentifier> _secrets;
    private readonly ScopedLogger logger;
    private readonly DebuggingRunSession debuggingSession;

    /// <summary>
    /// The run's debugging session, for the one other store that needs it.
    /// </summary>
    /// <remarks>
    /// Exposed here rather than threaded through the context's constructors because every view of the
    /// store already carries it, and a per-attempt view carries the same one — which is exactly the
    /// property a widget needs, since it is filed against the attempt rather than against a store.
    /// </remarks>
    internal DebuggingRunSession DebuggingSession => debuggingSession;

    /// <summary>
    /// Last published content fingerprint per variable. Only populated while something is capturing.
    /// </summary>
    private readonly Dictionary<VariableIdentifier, string> changeTokens;
    private readonly object changeTokenLock;

    /// <summary>
    /// Whether writes through this view of the store still count.
    /// </summary>
    /// <remarks>
    /// Unrestricted on the run's own store: writes that belong to no step - a fixture seeding a variable,
    /// the run publishing its summary - are always honoured.
    /// </remarks>
    private readonly RunWriter writer;

    /// <summary>
    /// Who this view writes as. Shared with the run's other channels through the context, so one gate decides
    /// for all of them.
    /// </summary>
    internal RunWriter Writer => writer;

    /// <summary>
    /// The run's own live things - what a package keeps for the length of a run and cannot put in a
    /// variable.
    /// </summary>
    /// <remarks>
    /// It hangs here because this is the one object that already means "this run", and it is shared by
    /// reference with every per-attempt view below, so a step, a retry of that step and the cleanup step
    /// after it all reach the same instance. A package that keyed its own table on whichever store it was
    /// handed would not - see <see cref="Runner.RunState"/> for what that cost.
    /// </remarks>
    public RunState RunState { get; }

    /// <summary>
    /// What this run decided on the caller's behalf, kept so the finished run can say so.
    /// </summary>
    /// <remarks>
    /// Here for the same reason <see cref="RunState"/> is: this is the one object that already means "this
    /// run", shared by reference with every per-attempt view, so a step, a retry of it and the cleanup step
    /// after all record into the same one.
    /// </remarks>
    public EffectiveSettings EffectiveSettings { get; }

    /// <summary>
    /// A view of this store that writes on behalf of one attempt at one step.
    /// </summary>
    /// <remarks>
    /// The same store and the same API - only the identity of the writer differs, which is what lets an
    /// abandoned attempt be told apart from the live one. A step therefore needs no new type and no new
    /// call shape; it just cannot write once the run has stopped waiting for it.
    /// </remarks>
    /// <param name="gate">The gate holding the current attempt.</param>
    /// <param name="attempt">The attempt this view writes for.</param>
    /// <returns>The view.</returns>
    internal VariableStore ForAttempt(StepAttemptGate gate, StepAttempt attempt)
        => new VariableStore(this, this.writer.Gate.For(gate, attempt));

    private VariableStore(VariableStore source, RunWriter writer)
    {
        // Shared by reference on purpose: one store, many writers, each able to say who it is.
        this.syncRoot = source.syncRoot;
        this._variables = source._variables;
        this._secrets = source._secrets;
        this.logger = source.logger;
        this.debuggingSession = source.debuggingSession;
        this.changeTokens = source.changeTokens;
        this.changeTokenLock = source.changeTokenLock;
        this.RunState = source.RunState.WrittenAs(writer);
        this.EffectiveSettings = source.EffectiveSettings.WrittenAs(writer);
        this.writer = writer;
    }

    internal VariableStore(ScopedLogger logger, DebuggingRunSession debuggingSession, RunWriteGate gate)
    {
        this.syncRoot = new object();
        this._variables = [];
        this._secrets = [];
        this.changeTokens = [];
        this.changeTokenLock = new object();
        this.logger = logger;
        this.debuggingSession = debuggingSession;
        this.writer = gate.Unrestricted;
        this.RunState = new RunState(this.writer, logger);
        this.EffectiveSettings = new EffectiveSettings(this.writer, logger);
    }

    /// <summary>
    /// Sets or replaces a variable value in the store.
    /// </summary>
    /// <typeparam name="T">The variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to set.</param>
    /// <param name="value">The value to store.</param>
    public void SetVariable<T>(VariableIdentifier identifier, T value)
    {
        // An attempt the runner has stopped waiting for is still running, and it must not be able to
        // reach the stores a later test reads.
        if (!writer.TryAdmit(RunChannel.Variable, identifier.Identifier, logger, out RunWriteTicket? ticket))
            return;

        // Hold the lock only for the dictionary read and write. Formatting a value can be arbitrarily
        // expensive, and it used to happen up to three times per write with the lock held.
        bool secret;
        using (ticket)
        lock (syncRoot)
        {
            _variables[identifier] = value;
            secret = _secrets.Contains(identifier);
        }

        if (!debuggingSession.IsCapturing)
            return;

        if (secret)
        {
            debuggingSession.PublishVariableUpdate(identifier, GetDebuggingStateFromValue(Secret, identifier));
            return;
        }

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
    /// Sets a variable whose value is a credential - a session cookie, a token - so the run uses it and nothing
    /// describes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value reaches whatever reads it, unchanged: a later step, and the test through <c>run.Variable&lt;T&gt;</c>.
    /// What changes is every place that <em>describes</em> it - the debugger's variable view, the value files a
    /// large value is written to, and an assertion's signal and failure text all say <c>(secret)</c>. It is the
    /// variable counterpart of a resource value a kind declares secret, and it exists for the same reason: a value
    /// read into a variable was the one road a captured credential could still take into a log in clear.
    /// </para>
    /// <para>
    /// Once secret, always secret for the run: a later <see cref="SetVariable{T}"/> under the same name keeps the mark,
    /// because the second value is very likely the same kind of thing as the first.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to set.</param>
    /// <param name="value">The value to store.</param>
    public void SetSecretVariable<T>(VariableIdentifier identifier, T value)
    {
        if (!writer.TryAdmit(RunChannel.Variable, identifier.Identifier, logger, out RunWriteTicket? ticket))
            return;

        using (ticket)
        lock (syncRoot)
        {
            _secrets.Add(identifier);
            _variables[identifier] = value;
        }

        if (debuggingSession.IsCapturing)
            debuggingSession.PublishVariableUpdate(identifier, GetDebuggingStateFromValue(Secret, identifier));
    }

    /// <summary>
    /// Whether the variable was written as a secret.
    /// </summary>
    /// <param name="identifier">The variable identifier.</param>
    /// <returns>Whether every description of it says <c>(secret)</c>.</returns>
    public bool IsSecret(VariableIdentifier identifier)
    {
        lock (syncRoot)
        {
            return _secrets.Contains(identifier);
        }
    }

    /// <summary>
    /// What a secret value is described as, everywhere.
    /// </summary>
    internal const string Secret = "(secret)";

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

    internal static DebugValue GetDebuggingStateFromValue(object? value, VariableIdentifier identifier)
        => GetDebuggingStateFromValue(value, identifier, DebugValueDescriber.Describe(value));

    private static DebugValue GetDebuggingStateFromValue(object? value, VariableIdentifier identifier, DescribedValue described)
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

    /// <summary>
    /// Gets a variable value without a static result type.
    /// </summary>
    /// <param name="identifier">The variable identifier to resolve.</param>
    /// <returns>The value.</returns>
    /// <exception cref="MissingVariableException">Nothing in this run set it.</exception>
    public object? GetVariable(VariableIdentifier identifier)
    {
        lock (syncRoot)
        {
            if (_variables.TryGetValue(identifier, out object? value))
                return value;
        }

        throw this.Missing(identifier);
    }

    /// <summary>
    /// Gets a variable value as a typed value.
    /// </summary>
    /// <typeparam name="T">The expected variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to resolve.</param>
    /// <returns>The value.</returns>
    /// <exception cref="MissingVariableException">Nothing in this run set it.</exception>
    /// <exception cref="VariableTypeMismatchException">It holds a different type.</exception>
    public T? GetVariable<T>(VariableIdentifier identifier)
    {
        object? value;

        lock (syncRoot)
        {
            if (!_variables.TryGetValue(identifier, out value))
                throw this.Missing(identifier);
        }

        return Cast<T>(identifier, value);
    }

    /// <summary>
    /// Says which variable is missing and which ones this run does have.
    /// </summary>
    /// <remarks>
    /// The dictionary lookup this replaces threw <c>KeyNotFoundException</c> - "the given key 'root' was
    /// not present in the dictionary" - which names neither the run nor anything a reader could act on.
    /// <see cref="MissingVariableException"/> had been written for exactly this and never thrown.
    /// </remarks>
    /// <param name="identifier">The variable that is not there.</param>
    /// <returns>The exception to throw.</returns>
    internal MissingVariableException Missing(VariableIdentifier identifier)
    {
        Dictionary<string, object?> available = [];

        lock (syncRoot)
        {
            foreach (KeyValuePair<VariableIdentifier, object?> entry in _variables)
            {
                available[entry.Key.Identifier] = entry.Value;
            }
        }

        return new MissingVariableException(identifier.Identifier, available);
    }

    /// <summary>
    /// Reads a value as the requested type, or says which variable disagreed.
    /// </summary>
    /// <typeparam name="T">The type the caller asked for.</typeparam>
    /// <param name="identifier">Which variable, for the message.</param>
    /// <param name="value">What it holds.</param>
    /// <returns>The value.</returns>
    private static T? Cast<T>(VariableIdentifier identifier, object? value)
    {
        if (value is null)
        {
            // A variable that was set to null reads as null, whatever type was asked for: it is a value
            // somebody stored on purpose, not a mismatch.
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        throw new VariableTypeMismatchException(identifier, typeof(T), value.GetType());
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
        object? raw;

        lock (syncRoot)
        {
            if (!_variables.TryGetValue(identifier, out raw))
            {
                value = default;
                return false;
            }
        }

        // Present but the wrong type is still a mistake worth naming. TryGet answers "is it there", not
        // "is it whatever I claimed" - silently returning default here would hide a real disagreement.
        value = Cast<T>(identifier, raw);

        return true;
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