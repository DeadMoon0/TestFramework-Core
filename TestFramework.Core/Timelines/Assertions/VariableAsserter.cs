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

    internal VariableAsserter(T? value, bool exists, string name, ScopedLogger? logger)
    {
        _value = value;
        _exists = exists;
        _display = $"'{name}'";
        _logger = logger;
    }

    private VariableAsserter<T> Pass(string assertion)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Variable, _display, assertion, [], true, _exists ? _value : "<unset>");
        return this;
    }

    private VariableAsserter<T> Fail(string assertion, string reason)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Variable, _display, assertion, [], false, _exists ? _value : "<unset>");
        var message = $"Variable {_display}: {assertion} failed \u2014 {reason}";
        if (_logger?.CurrentScope is { } scope)
        {
            scope.RecordFailure(message);
            return this;
        }
        throw new ValueAssertionException(message);
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
            return Fail(nameof(NotExist), $"variable exists with value '{_value}'");
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
    /// with the reason <see cref="Exist"/> gives.
    /// </remarks>
    /// <param name="assertion">The assertion being made, for the failure message.</param>
    /// <returns>The value asserter, or <see langword="null"/> once the missing value has been reported.</returns>
    private ValueAsserter<T?>? ValueOrFail([System.Runtime.CompilerServices.CallerMemberName] string assertion = "")
    {
        if (_exists)
            return new ValueAsserter<T?>(_value, _display, _logger);

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
    /// <param name="description">A short description included in assertion output.</param>
    public VariableAsserter<T> Match(System.Func<T?, bool> predicate, string description = "custom predicate")
    { ValueOrFail()?.Match(predicate, description); return this; }

    /// <summary>
    /// Asserts that the variable value does not satisfy the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public VariableAsserter<T> NotMatch(System.Func<T?, bool> predicate, string description = "custom predicate")
    { ValueOrFail()?.NotMatch(predicate, description); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation contains the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must be present.</param>
    public VariableAsserter<T> Contain(string substring) { ValueOrFail()?.Contain(substring); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation does not contain the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must not be present.</param>
    public VariableAsserter<T> NotContain(string substring) { ValueOrFail()?.NotContain(substring); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation starts with the specified prefix.
    /// </summary>
    /// <param name="prefix">The required prefix.</param>
    public VariableAsserter<T> StartWith(string prefix) { ValueOrFail()?.StartWith(prefix); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation ends with the specified suffix.
    /// </summary>
    /// <param name="suffix">The required suffix.</param>
    public VariableAsserter<T> EndWith(string suffix) { ValueOrFail()?.EndWith(suffix); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation is empty.
    /// </summary>
    public VariableAsserter<T> BeEmpty() { ValueOrFail()?.BeEmpty(); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation is not empty.
    /// </summary>
    public VariableAsserter<T> NotBeEmpty() { ValueOrFail()?.NotBeEmpty(); return this; }

    /// <summary>
    /// Asserts that the variable value is greater than the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower exclusive bound.</param>
    public VariableAsserter<T> BeGreaterThan(T threshold) { ValueOrFail()?.BeGreaterThan(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value is greater than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower inclusive bound.</param>
    public VariableAsserter<T> BeGreaterThanOrEqualTo(T threshold) { ValueOrFail()?.BeGreaterThanOrEqualTo(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value is less than the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper exclusive bound.</param>
    public VariableAsserter<T> BeLessThan(T threshold) { ValueOrFail()?.BeLessThan(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value is less than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper inclusive bound.</param>
    public VariableAsserter<T> BeLessThanOrEqualTo(T threshold) { ValueOrFail()?.BeLessThanOrEqualTo(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value falls within the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public VariableAsserter<T> BeInRange(T min, T max) { ValueOrFail()?.BeInRange(min, max); return this; }

    /// <summary>
    /// Asserts that the variable value falls outside the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public VariableAsserter<T> NotBeInRange(T min, T max) { ValueOrFail()?.NotBeInRange(min, max); return this; }

    /// <summary>
    /// Asserts that the variable value has the expected number of items.
    /// </summary>
    /// <param name="expected">The expected item count.</param>
    public VariableAsserter<T> HaveCount(int expected) { ValueOrFail()?.HaveCount(expected); return this; }

    /// <summary>
    /// Asserts that the variable value contains no items.
    /// </summary>
    public VariableAsserter<T> HaveNoItems() { ValueOrFail()?.HaveNoItems(); return this; }

    /// <summary>
    /// Asserts that the variable value contains at least one item.
    /// </summary>
    public VariableAsserter<T> HaveItems() { ValueOrFail()?.HaveItems(); return this; }

    // ── Chaining ────────────────────────────────────────────────────────────

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public VariableAsserter<T> And() => this;
}
