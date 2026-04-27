using System;
using System.Collections;
using System.Linq;
using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Provides fluent assertions for a concrete value.
/// </summary>
/// <typeparam name="T">The asserted value type.</typeparam>
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

    /// <summary>
    /// Asserts that the value is <see langword="null"/>.
    /// </summary>
    public ValueAsserter<T> BeNull()
    {
        if (_value is not null)
            return Fail(nameof(BeNull), $"was {V(_value)}");
        return Pass(nameof(BeNull));
    }

    /// <summary>
    /// Asserts that the value is not <see langword="null"/>.
    /// </summary>
    public ValueAsserter<T> NotBeNull()
    {
        if (_value is null)
            return Fail(nameof(NotBeNull), "was null");
        return Pass(nameof(NotBeNull));
    }

    // ── Equality ────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the value equals the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    public ValueAsserter<T> Be(T expected)
    {
        if (!Equals(_value, expected))
            return Fail(nameof(Be), $"expected {V(expected)}, was {V(_value)}");
        return Pass($"{nameof(Be)}({V(expected)})");
    }

    /// <summary>
    /// Asserts that the value does not equal the expected value.
    /// </summary>
    /// <param name="expected">The value that must not match.</param>
    public ValueAsserter<T> NotBe(T expected)
    {
        if (Equals(_value, expected))
            return Fail(nameof(NotBe), $"expected not {V(expected)}");
        return Pass($"{nameof(NotBe)}({V(expected)})");
    }

    // ── Predicate ───────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the value satisfies the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="true"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public ValueAsserter<T> Match(Func<T, bool> predicate, string description = "custom predicate")
    {
        if (!predicate(_value))
            return Fail(nameof(Match), $"value {V(_value)} did not satisfy: {description}");
        return Pass($"{nameof(Match)}({description})");
    }

    /// <summary>
    /// Asserts that the value does not satisfy the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public ValueAsserter<T> NotMatch(Func<T, bool> predicate, string description = "custom predicate")
    {
        if (predicate(_value))
            return Fail(nameof(NotMatch), $"value {V(_value)} unexpectedly satisfied: {description}");
        return Pass($"{nameof(NotMatch)}({description})");
    }

    // ── String ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the string representation contains the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must be present.</param>
    public ValueAsserter<T> Contain(string substring)
    {
        var s = _value?.ToString() ?? "";
        if (!s.Contains(substring))
            return Fail(nameof(Contain), $"{V(_value)} does not contain \"{substring}\"");
        return Pass($"{nameof(Contain)}(\"{substring}\")");
    }

    /// <summary>
    /// Asserts that the string representation does not contain the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must not be present.</param>
    public ValueAsserter<T> NotContain(string substring)
    {
        var s = _value?.ToString() ?? "";
        if (s.Contains(substring))
            return Fail(nameof(NotContain), $"{V(_value)} contains \"{substring}\"");
        return Pass($"{nameof(NotContain)}(\"{substring}\")");
    }

    /// <summary>
    /// Asserts that the string representation starts with the specified prefix.
    /// </summary>
    /// <param name="prefix">The required prefix.</param>
    public ValueAsserter<T> StartWith(string prefix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.StartsWith(prefix, StringComparison.Ordinal))
            return Fail(nameof(StartWith), $"{V(_value)} does not start with \"{prefix}\"");
        return Pass($"{nameof(StartWith)}(\"{prefix}\")");
    }

    /// <summary>
    /// Asserts that the string representation ends with the specified suffix.
    /// </summary>
    /// <param name="suffix">The required suffix.</param>
    public ValueAsserter<T> EndWith(string suffix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.EndsWith(suffix, StringComparison.Ordinal))
            return Fail(nameof(EndWith), $"{V(_value)} does not end with \"{suffix}\"");
        return Pass($"{nameof(EndWith)}(\"{suffix}\")");
    }

    /// <summary>
    /// Asserts that the string representation is empty.
    /// </summary>
    public ValueAsserter<T> BeEmpty()
    {
        var s = _value?.ToString() ?? "";
        if (s.Length != 0)
            return Fail(nameof(BeEmpty), $"was {V(_value)}");
        return Pass(nameof(BeEmpty));
    }

    /// <summary>
    /// Asserts that the string representation is not empty.
    /// </summary>
    public ValueAsserter<T> NotBeEmpty()
    {
        var s = _value?.ToString() ?? "";
        if (s.Length == 0)
            return Fail(nameof(NotBeEmpty), "was empty");
        return Pass(nameof(NotBeEmpty));
    }

    // ── Numeric / Comparable ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the value is greater than the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower exclusive bound.</param>
    public ValueAsserter<T> BeGreaterThan(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) <= 0)
            return Fail(nameof(BeGreaterThan), $"expected > {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeGreaterThan)}({V(threshold)})");
    }

    /// <summary>
    /// Asserts that the value is greater than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower inclusive bound.</param>
    public ValueAsserter<T> BeGreaterThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) < 0)
            return Fail(nameof(BeGreaterThanOrEqualTo), $"expected >= {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeGreaterThanOrEqualTo)}({V(threshold)})");
    }

    /// <summary>
    /// Asserts that the value is less than the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper exclusive bound.</param>
    public ValueAsserter<T> BeLessThan(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) >= 0)
            return Fail(nameof(BeLessThan), $"expected < {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeLessThan)}({V(threshold)})");
    }

    /// <summary>
    /// Asserts that the value is less than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper inclusive bound.</param>
    public ValueAsserter<T> BeLessThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) > 0)
            return Fail(nameof(BeLessThanOrEqualTo), $"expected <= {V(threshold)}, was {V(_value)}");
        return Pass($"{nameof(BeLessThanOrEqualTo)}({V(threshold)})");
    }

    /// <summary>
    /// Asserts that the value falls within the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public ValueAsserter<T> BeInRange(T min, T max)
    {
        if (_value is not IComparable<T> c || c.CompareTo(min) < 0 || c.CompareTo(max) > 0)
            return Fail(nameof(BeInRange), $"expected in [{V(min)}, {V(max)}], was {V(_value)}");
        return Pass($"{nameof(BeInRange)}([{V(min)}, {V(max)}])");
    }

    /// <summary>
    /// Asserts that the value falls outside the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public ValueAsserter<T> NotBeInRange(T min, T max)
    {
        if (_value is IComparable<T> c && c.CompareTo(min) >= 0 && c.CompareTo(max) <= 0)
            return Fail(nameof(NotBeInRange), $"expected outside [{V(min)}, {V(max)}], was {V(_value)}");
        return Pass($"{nameof(NotBeInRange)}([{V(min)}, {V(max)}])");
    }

    // ── Collection ──────────────────────────────────────────────────────────

    private static int CountItems(T value)
        => value is ICollection col ? col.Count : ((IEnumerable)value!).Cast<object>().Count();

    /// <summary>
    /// Asserts that the collection has the expected number of items.
    /// </summary>
    /// <param name="expected">The expected item count.</param>
    public ValueAsserter<T> HaveCount(int expected)
    {
        int count = CountItems(_value);
        if (count != expected)
            return Fail(nameof(HaveCount), $"expected {expected} element(s), was {count}");
        return Pass($"{nameof(HaveCount)}({expected})");
    }

    /// <summary>
    /// Asserts that the collection contains no items.
    /// </summary>
    public ValueAsserter<T> HaveNoItems()
    {
        int count = CountItems(_value);
        if (count != 0)
            return Fail(nameof(HaveNoItems), $"expected empty, had {count} element(s)");
        return Pass(nameof(HaveNoItems));
    }

    /// <summary>
    /// Asserts that the collection contains at least one item.
    /// </summary>
    public ValueAsserter<T> HaveItems()
    {
        int count = CountItems(_value);
        if (count == 0)
            return Fail(nameof(HaveItems), "was empty");
        return Pass(nameof(HaveItems));
    }

    // ── Chaining ────────────────────────────────────────────────────────────

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public ValueAsserter<T> And() => this;
}
