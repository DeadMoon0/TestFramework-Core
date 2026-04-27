using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;

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
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_display}  {assertion}  [PASS]");
        return this;
    }

    private VariableAsserter<T> Fail(string assertion, string reason)
    {
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_display}  {assertion}  [FAIL]  {reason}");
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

    private ValueAsserter<T?> ValueAsserter() => new ValueAsserter<T?>(_value, _display, _logger);

    /// <summary>
    /// Asserts that the variable value is <see langword="null"/>.
    /// </summary>
    public VariableAsserter<T> BeNull() { ValueAsserter().BeNull(); return this; }

    /// <summary>
    /// Asserts that the variable value is not <see langword="null"/>.
    /// </summary>
    public VariableAsserter<T> NotBeNull() { ValueAsserter().NotBeNull(); return this; }

    /// <summary>
    /// Asserts that the variable value equals the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    public VariableAsserter<T> Be(T expected) { ValueAsserter().Be(expected); return this; }

    /// <summary>
    /// Asserts that the variable value does not equal the expected value.
    /// </summary>
    /// <param name="expected">The value that must not match.</param>
    public VariableAsserter<T> NotBe(T expected) { ValueAsserter().NotBe(expected); return this; }

    /// <summary>
    /// Asserts that the variable value satisfies the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="true"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public VariableAsserter<T> Match(System.Func<T?, bool> predicate, string description = "custom predicate")
    { ValueAsserter().Match(predicate, description); return this; }

    /// <summary>
    /// Asserts that the variable value does not satisfy the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public VariableAsserter<T> NotMatch(System.Func<T?, bool> predicate, string description = "custom predicate")
    { ValueAsserter().NotMatch(predicate, description); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation contains the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must be present.</param>
    public VariableAsserter<T> Contain(string substring) { ValueAsserter().Contain(substring); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation does not contain the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must not be present.</param>
    public VariableAsserter<T> NotContain(string substring) { ValueAsserter().NotContain(substring); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation starts with the specified prefix.
    /// </summary>
    /// <param name="prefix">The required prefix.</param>
    public VariableAsserter<T> StartWith(string prefix) { ValueAsserter().StartWith(prefix); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation ends with the specified suffix.
    /// </summary>
    /// <param name="suffix">The required suffix.</param>
    public VariableAsserter<T> EndWith(string suffix) { ValueAsserter().EndWith(suffix); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation is empty.
    /// </summary>
    public VariableAsserter<T> BeEmpty() { ValueAsserter().BeEmpty(); return this; }

    /// <summary>
    /// Asserts that the variable value's string representation is not empty.
    /// </summary>
    public VariableAsserter<T> NotBeEmpty() { ValueAsserter().NotBeEmpty(); return this; }

    /// <summary>
    /// Asserts that the variable value is greater than the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower exclusive bound.</param>
    public VariableAsserter<T> BeGreaterThan(T threshold) { ValueAsserter().BeGreaterThan(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value is greater than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower inclusive bound.</param>
    public VariableAsserter<T> BeGreaterThanOrEqualTo(T threshold) { ValueAsserter().BeGreaterThanOrEqualTo(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value is less than the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper exclusive bound.</param>
    public VariableAsserter<T> BeLessThan(T threshold) { ValueAsserter().BeLessThan(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value is less than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper inclusive bound.</param>
    public VariableAsserter<T> BeLessThanOrEqualTo(T threshold) { ValueAsserter().BeLessThanOrEqualTo(threshold); return this; }

    /// <summary>
    /// Asserts that the variable value falls within the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public VariableAsserter<T> BeInRange(T min, T max) { ValueAsserter().BeInRange(min, max); return this; }

    /// <summary>
    /// Asserts that the variable value falls outside the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public VariableAsserter<T> NotBeInRange(T min, T max) { ValueAsserter().NotBeInRange(min, max); return this; }

    /// <summary>
    /// Asserts that the variable value has the expected number of items.
    /// </summary>
    /// <param name="expected">The expected item count.</param>
    public VariableAsserter<T> HaveCount(int expected) { ValueAsserter().HaveCount(expected); return this; }

    /// <summary>
    /// Asserts that the variable value contains no items.
    /// </summary>
    public VariableAsserter<T> HaveNoItems() { ValueAsserter().HaveNoItems(); return this; }

    /// <summary>
    /// Asserts that the variable value contains at least one item.
    /// </summary>
    public VariableAsserter<T> HaveItems() { ValueAsserter().HaveItems(); return this; }

    // ── Chaining ────────────────────────────────────────────────────────────

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public VariableAsserter<T> And() => this;
}
