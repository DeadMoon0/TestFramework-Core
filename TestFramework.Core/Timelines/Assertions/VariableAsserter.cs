using TestFramework.Core.Logging;
using TestFrameworkCore.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

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

    public VariableAsserter<T> Exist()
    {
        if (!_exists)
            return Fail(nameof(Exist), "variable was not set");
        return Pass(nameof(Exist));
    }

    public VariableAsserter<T> NotExist()
    {
        if (_exists)
            return Fail(nameof(NotExist), $"variable exists with value '{_value}'");
        return Pass(nameof(NotExist));
    }

    // ── Value — delegates to ValueAsserter ───────────────────────────────────
    // This keeps one source of truth for all assertion logic.

    private ValueAsserter<T?> ValueAsserter() => new ValueAsserter<T?>(_value, _display, _logger);

    public VariableAsserter<T> BeNull() { ValueAsserter().BeNull(); return this; }
    public VariableAsserter<T> NotBeNull() { ValueAsserter().NotBeNull(); return this; }
    public VariableAsserter<T> Be(T expected) { ValueAsserter().Be(expected); return this; }
    public VariableAsserter<T> NotBe(T expected) { ValueAsserter().NotBe(expected); return this; }

    public VariableAsserter<T> Match(System.Func<T?, bool> predicate, string description = "custom predicate")
    { ValueAsserter().Match(predicate, description); return this; }

    public VariableAsserter<T> NotMatch(System.Func<T?, bool> predicate, string description = "custom predicate")
    { ValueAsserter().NotMatch(predicate, description); return this; }

    public VariableAsserter<T> Contain(string substring) { ValueAsserter().Contain(substring); return this; }
    public VariableAsserter<T> NotContain(string substring) { ValueAsserter().NotContain(substring); return this; }
    public VariableAsserter<T> StartWith(string prefix) { ValueAsserter().StartWith(prefix); return this; }
    public VariableAsserter<T> EndWith(string suffix) { ValueAsserter().EndWith(suffix); return this; }
    public VariableAsserter<T> BeEmpty() { ValueAsserter().BeEmpty(); return this; }
    public VariableAsserter<T> NotBeEmpty() { ValueAsserter().NotBeEmpty(); return this; }

    public VariableAsserter<T> BeGreaterThan(T threshold) { ValueAsserter().BeGreaterThan(threshold); return this; }
    public VariableAsserter<T> BeGreaterThanOrEqualTo(T threshold) { ValueAsserter().BeGreaterThanOrEqualTo(threshold); return this; }
    public VariableAsserter<T> BeLessThan(T threshold) { ValueAsserter().BeLessThan(threshold); return this; }
    public VariableAsserter<T> BeLessThanOrEqualTo(T threshold) { ValueAsserter().BeLessThanOrEqualTo(threshold); return this; }
    public VariableAsserter<T> BeInRange(T min, T max) { ValueAsserter().BeInRange(min, max); return this; }
    public VariableAsserter<T> NotBeInRange(T min, T max) { ValueAsserter().NotBeInRange(min, max); return this; }

    public VariableAsserter<T> HaveCount(int expected) { ValueAsserter().HaveCount(expected); return this; }
    public VariableAsserter<T> HaveNoItems() { ValueAsserter().HaveNoItems(); return this; }
    public VariableAsserter<T> HaveItems() { ValueAsserter().HaveItems(); return this; }

    // ── Chaining ────────────────────────────────────────────────────────────

    public VariableAsserter<T> And() => this;
}
