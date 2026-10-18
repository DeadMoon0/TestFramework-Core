using TestFramework.Core.Variables;
using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Provides fluent assertions for a variable lookup result.
/// </summary>
/// <typeparam name="T">The variable value type.</typeparam>
public class VariableAsserter<T>
{
    private readonly T? _value;
    private readonly bool _exists;
    private readonly string _display;
    private readonly ScopedLogger? _logger;
    private readonly bool _secret;

    internal VariableAsserter(T? value, bool exists, string name, ScopedLogger? logger, bool secret)
    {
        _secret = secret;
        _value = value;
        _exists = exists;
        _display = $"'{name}'";
        _logger = logger;
    }

    // A secret variable is asserted on like any other; only what the assertion says about it is redacted.
    private object? Shown => _secret ? VariableStore.Secret : _value;

    private VariableAsserter<T> Pass(string assertion)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Variable, _display, assertion, [], true, _exists ? Shown : "<unset>");
        return this;
    }

    private VariableAsserter<T> Fail(string assertion, string reason)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Variable, _display, assertion, [], false, _exists ? Shown : "<unset>");
        var message = $"Variable {_display}: {assertion} failed \u2014 {reason}";
        AssertionFailure.Raise(_logger, message, static (text, site) => new ValueAssertionException(text, site));
        return this;
    }

    // ── Existence ────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the variable exists in the run store.
    /// </summary>
    public VariableAsserter<T> Exist()
    {
        if (!_exists)
            return Fail(nameof(Exist), "variable was not set");
        return Pass(nameof(Exist));
    }

    /// <summary>
    /// Asserts that the variable does not exist in the run store.
    /// </summary>
    public VariableAsserter<T> NotExist()
    {
        if (_exists)
            return Fail(nameof(NotExist), $"variable exists with value '{Shown}'");
        return Pass(nameof(NotExist));
    }

    // ── Value — delegates to ValueAsserter ───────────────────────────────────
    // This keeps one source of truth for all assertion logic.

    /// <summary>
    /// The asserter for the variable's value, or a failed assertion when there is no value to assert on.
    /// </summary>
    /// <remarks>
    /// A variable that was never set has no value, and handing <c>default</c> on as if it were one is how
    /// <c>Be(0)</c>, <c>Be(false)</c> and <c>BeNull()</c> used to pass for data that did not exist. Only
    /// <see cref="NotExist"/> passes for a missing variable; every assertion about its value fails first,
    /// with the reason <see cref="Exist"/> gives. Internal so the type-specific assertions in
    /// <see cref="VariableAssertions"/> go through the same check.
    /// </remarks>
    /// <param name="assertion">The assertion being made, for the failure message.</param>
    /// <returns>The value asserter, or <see langword="null"/> once the missing value has been reported.</returns>
    internal ValueAsserter<T>? ValueOrFail([System.Runtime.CompilerServices.CallerMemberName] string assertion = "")
    {
        if (_exists)
            return new ValueAsserter<T>(_value!, _display, _logger, _secret);

        Fail(assertion, "variable was not set");
        return null;
    }

    /// <summary>
    /// Asserts that the variable value is <see langword="null"/>.
    /// </summary>
    public VariableAsserter<T> BeNull() { ValueOrFail()?.BeNull(); return this; }

    /// <summary>
    /// Asserts that the variable value is not <see langword="null"/>.
    /// </summary>
    public VariableAsserter<T> NotBeNull() { ValueOrFail()?.NotBeNull(); return this; }

    /// <summary>
    /// Asserts that the variable value equals the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    public VariableAsserter<T> Be(T expected) { ValueOrFail()?.Be(expected); return this; }

    /// <summary>
    /// Asserts that the variable value does not equal the expected value.
    /// </summary>
    /// <param name="expected">The value that must not match.</param>
    public VariableAsserter<T> NotBe(T expected) { ValueOrFail()?.NotBe(expected); return this; }

    /// <summary>
    /// Asserts that the variable value satisfies the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="true"/>.</param>
    public VariableAsserter<T> Match(System.Func<T?, bool> predicate)
    { ValueOrFail()?.Match(predicate); return this; }

    /// <summary>
    /// Asserts that the variable value satisfies the provided predicate, saying what it checks.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="true"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public VariableAsserter<T> Match(System.Func<T?, bool> predicate, string description)
    { ValueOrFail()?.Match(predicate, description); return this; }

    /// <summary>
    /// Asserts that the variable value does not satisfy the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    public VariableAsserter<T> NotMatch(System.Func<T?, bool> predicate)
    { ValueOrFail()?.NotMatch(predicate); return this; }

    /// <summary>
    /// Asserts that the variable value does not satisfy the provided predicate, saying what it checks.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public VariableAsserter<T> NotMatch(System.Func<T?, bool> predicate, string description)
    { ValueOrFail()?.NotMatch(predicate, description); return this; }

    // ── Chaining ────────────────────────────────────────────────────────────

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public VariableAsserter<T> And() => this;
}
