using System;
using System.Collections;
using System.Linq;
using TestFrameworkCore.Exceptions;
using TestFrameworkCore.Logging;

namespace TestFrameworkCore.Timelines.Assertions;

public class ValueAsserter<T>
{
    private readonly T _value;
    private readonly string _expression;
    private readonly ScopedLogger? _logger;

    internal ValueAsserter(T value, string expression, ScopedLogger? logger)
    {
        _value = value;
        _expression = expression;
        _logger = logger;
    }

    // Wraps any value in a FormattableValue so string interpolation uses
    // VariableFormatter instead of plain ToString().
    private static FormattableValue<T> V(T v) => new(v);

    private ValueAsserter<T> Pass(string assertion)
    {
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_expression}  {assertion}  [PASS]");
        return this;
    }

    private ValueAsserter<T> Fail(string assertion, string reason)
    {
        _logger?.EnsureAssertionHeaderPrinted();
        _logger?.LogInformation($"[ASSERT]  {_expression}  {assertion}  [FAIL]  {reason}");
        var message = $"{_expression}: {assertion} failed \u2014 {reason}";
        if (_logger?.CurrentScope is { } scope)
        {
            scope.RecordFailure(message);
            return this;
        }
        throw new ValueAssertionException(message);
    }

    // ── Null ────────────────────────────────────────────────────────────────

    public ValueAsserter<T> BeNull()
    {
        if (_value is not null)
            return Fail(nameof(BeNull), $"was {V(_value)}");
        return Pass(nameof(BeNull));
    }

    public ValueAsserter<T> NotBeNull()
    {
        if (_value is null)
            return Fail(nameof(NotBeNull), "was null");
        return Pass(nameof(NotBeNull));
    }

    // ── Equality ────────────────────────────────────────────────────────────

    public ValueAsserter<T> Be(T expected)
    {
        if (!Equals(_value, expected))
            return Fail(nameof(Be), $"expected {V(expected)}, was {V(_value)}");
        return Pass($"{nameof(Be)}({V(expected)})");
    }

    public ValueAsserter<T> NotBe(T expected)
    {
        if (Equals(_value, expected))
            return Fail(nameof(NotBe), $"expected not {V(expected)}");
        return Pass($"{nameof(NotBe)}({V(expected)})");
    }

    // ── Predicate ───────────────────────────────────────────────────────────

    public ValueAsserter<T> Match(Func<T, bool> predicate, string description = "custom predicate")
    {
        if (!predicate(_value))
            return Fail(nameof(Match), $"value {V(_value)} did not satisfy: {description}");
        return Pass($"{nameof(Match)}({description})");
    }

    public ValueAsserter<T> NotMatch(Func<T, bool> predicate, string description = "custom predicate")
    {
        if (predicate(_value))
            return Fail(nameof(NotMatch), $"value {V(_value)} unexpectedly satisfied: {description}");
        return Pass($"{nameof(NotMatch)}({description})");
    }

    // ── String ──────────────────────────────────────────────────────────────

    public ValueAsserter<T> Contain(string substring)
    {
        var s = _value?.ToString() ?? "";
        if (!s.Contains(substring))
            return Fail(nameof(Contain), $"{V(_value)} does not contain \"{substring}\"");
        return Pass($"{nameof(Contain)}(\"{substring}\")");
    }

    public ValueAsserter<T> NotContain(string substring)
    {
        var s = _value?.ToString() ?? "";
        if (s.Contains(substring))
            return Fail(nameof(NotContain), $"{V(_value)} contains \"{substring}\"");
        return Pass($"{nameof(NotContain)}(\"{substring}\")");
    }

    public ValueAsserter<T> StartWith(string prefix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.StartsWith(prefix, StringComparison.Ordinal))
            return Fail(nameof(StartWith), $"{V(_value)} does not start with \"{prefix}\"");
        return Pass($"{nameof(StartWith)}(\"{prefix}\")");
    }

    public ValueAsserter<T> EndWith(string suffix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.EndsWith(suffix, StringComparison.Ordinal))
            return Fail(nameof(EndWith), $"{V(_value)} does not end with \"{suffix}\"");
        return Pass($"{nameof(EndWith)}(\"{suffix}\")");
    }

    public ValueAsserter<T> BeEmpty()
    {
        var s = _value?.ToString() ?? "";
        if (s.Length != 0)
            return Fail(nameof(BeEmpty), $"was {V(_value)}");
        return Pass(nameof(BeEmpty));
    }

    public ValueAsserter<T> NotBeEmpty()
    {
        var s = _value?.ToString() ?? "";
        if (s.Length == 0)
            return Fail(nameof(NotBeEmpty), "was empty");
        return Pass(nameof(NotBeEmpty));
    }

    // ── Numeric / Comparable ────────────────────────────────────────────────

    public ValueAsserter<T> BeGreaterThan(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) <= 0)
            return Fail(nameof(BeGreaterThan), $"expected > {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeGreaterThan)}({V(threshold)})");
    }

    public ValueAsserter<T> BeGreaterThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) < 0)
            return Fail(nameof(BeGreaterThanOrEqualTo), $"expected >= {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeGreaterThanOrEqualTo)}({V(threshold)})");
    }

    public ValueAsserter<T> BeLessThan(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) >= 0)
            return Fail(nameof(BeLessThan), $"expected < {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeLessThan)}({V(threshold)})");
    }

    public ValueAsserter<T> BeLessThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) > 0)
            return Fail(nameof(BeLessThanOrEqualTo), $"expected <= {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeLessThanOrEqualTo)}({V(threshold)})");
    }

    public ValueAsserter<T> BeInRange(T min, T max)
    {
        if (_value is not IComparable<T> c || c.CompareTo(min) < 0 || c.CompareTo(max) > 0)
            return Fail(nameof(BeInRange), $"expected in [{V(min)}, {V(max)}], was {V(_value)}");
        return Pass($"{nameof(BeInRange)}([{V(min)}, {V(max)}])");
    }

    public ValueAsserter<T> NotBeInRange(T min, T max)
    {
        if (_value is IComparable<T> c && c.CompareTo(min) >= 0 && c.CompareTo(max) <= 0)
            return Fail(nameof(NotBeInRange), $"expected outside [{V(min)}, {V(max)}], was {V(_value)}");
        return Pass($"{nameof(NotBeInRange)}([{V(min)}, {V(max)}])");
    }

    // ── Collection ──────────────────────────────────────────────────────────

    private static int CountItems(T value)
        => value is ICollection col ? col.Count : ((IEnumerable)value!).Cast<object>().Count();

    public ValueAsserter<T> HaveCount(int expected)
    {
        int count = CountItems(_value);
        if (count != expected)
            return Fail(nameof(HaveCount), $"expected {expected} element(s), was {count}");
        return Pass($"{nameof(HaveCount)}({expected})");
    }

    public ValueAsserter<T> HaveNoItems()
    {
        int count = CountItems(_value);
        if (count != 0)
            return Fail(nameof(HaveNoItems), $"expected empty, had {count} element(s)");
        return Pass(nameof(HaveNoItems));
    }

    public ValueAsserter<T> HaveItems()
    {
        int count = CountItems(_value);
        if (count == 0)
            return Fail(nameof(HaveItems), "was empty");
        return Pass(nameof(HaveItems));
    }

    // ── Chaining ────────────────────────────────────────────────────────────

    public ValueAsserter<T> And() => this;
}
