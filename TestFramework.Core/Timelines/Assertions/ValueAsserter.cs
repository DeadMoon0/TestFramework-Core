using System;
using System.Collections;
using System.Linq;
using TestFramework.Core.Debugger;
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

    private static string Render(object? value) => value?.ToString() ?? "null";

    /// <summary>The check as it would be written in a test, for a human reading a failure.</summary>
    private static string Call(string assertionName, (string Name, object? Value)[] arguments)
        => arguments.Length == 0
            ? assertionName
            : $"{assertionName}({string.Join(", ", arguments.Select(argument => VariableFormatter.Format(argument.Value)))})";

    private ValueAsserter<T> Pass(string assertionName, params (string Name, object? Value)[] arguments)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Value, _expression, assertionName, arguments, true, _value);
        return this;
    }

    private ValueAsserter<T> Fail(string assertionName, string reason, params (string Name, object? Value)[] arguments)
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Value, _expression, assertionName, arguments, false, _value);

        // The thrown message is for whoever is reading a failed test, so it is rendered here - from the same
        // name and arguments the transport carries, rather than from a second string built beside them.
        var message = $"{_expression}: {Call(assertionName, arguments)} failed \u2014 {reason}";
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
            return Fail(nameof(Be), $"expected {V(expected)}, was {V(_value)}", ("expected", expected));
        return Pass(nameof(Be), ("expected", expected));
    }

    /// <summary>
    /// Asserts that the value does not equal the expected value.
    /// </summary>
    /// <param name="expected">The value that must not match.</param>
    public ValueAsserter<T> NotBe(T expected)
    {
        if (Equals(_value, expected))
            return Fail(nameof(NotBe), $"expected not {V(expected)}", ("expected", expected));
        return Pass(nameof(NotBe), ("expected", expected));
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
            return Fail(nameof(Match), $"value {V(_value)} did not satisfy: {description}", ("description", description));
        return Pass(nameof(Match), ("description", description));
    }

    /// <summary>
    /// Asserts that the value does not satisfy the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public ValueAsserter<T> NotMatch(Func<T, bool> predicate, string description = "custom predicate")
    {
        if (predicate(_value))
            return Fail(nameof(NotMatch), $"value {V(_value)} unexpectedly satisfied: {description}", ("description", description));
        return Pass(nameof(NotMatch), ("description", description));
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
            return Fail(nameof(Contain), $"{V(_value)} does not contain \"{substring}\"", ("substring", substring));
        return Pass(nameof(Contain), ("substring", substring));
    }

    /// <summary>
    /// Asserts that the string representation does not contain the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must not be present.</param>
    public ValueAsserter<T> NotContain(string substring)
    {
        var s = _value?.ToString() ?? "";
        if (s.Contains(substring))
            return Fail(nameof(NotContain), $"{V(_value)} contains \"{substring}\"", ("substring", substring));
        return Pass(nameof(NotContain), ("substring", substring));
    }

    /// <summary>
    /// Asserts that the string representation starts with the specified prefix.
    /// </summary>
    /// <param name="prefix">The required prefix.</param>
    public ValueAsserter<T> StartWith(string prefix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.StartsWith(prefix, StringComparison.Ordinal))
            return Fail(nameof(StartWith), $"{V(_value)} does not start with \"{prefix}\"", ("prefix", prefix));
        return Pass(nameof(StartWith), ("prefix", prefix));
    }

    /// <summary>
    /// Asserts that the string representation ends with the specified suffix.
    /// </summary>
    /// <param name="suffix">The required suffix.</param>
    public ValueAsserter<T> EndWith(string suffix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.EndsWith(suffix, StringComparison.Ordinal))
            return Fail(nameof(EndWith), $"{V(_value)} does not end with \"{suffix}\"", ("suffix", suffix));
        return Pass(nameof(EndWith), ("suffix", suffix));
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
            return Fail(nameof(BeGreaterThan), $"expected > {V(threshold)}, was {V(_value)}", ("threshold", threshold));
        return Pass(nameof(BeGreaterThan), ("threshold", threshold));
    }

    /// <summary>
    /// Asserts that the value is greater than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower inclusive bound.</param>
    public ValueAsserter<T> BeGreaterThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) < 0)
            return Fail(nameof(BeGreaterThanOrEqualTo), $"expected >= {V(threshold)}, was {V(_value)}", ("threshold", threshold));
        return Pass(nameof(BeGreaterThanOrEqualTo), ("threshold", threshold));
    }

    /// <summary>
    /// Asserts that the value is less than the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper exclusive bound.</param>
    public ValueAsserter<T> BeLessThan(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) >= 0)
            return Fail(nameof(BeLessThan), $"expected < {V(threshold)}, was {V(_value)}", ("threshold", threshold));
        return Pass(nameof(BeLessThan), ("threshold", threshold));
    }

    /// <summary>
    /// Asserts that the value is less than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper inclusive bound.</param>
    public ValueAsserter<T> BeLessThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) > 0)
            return Fail(nameof(BeLessThanOrEqualTo), $"expected <= {V(threshold)}, was {V(_value)}", ("threshold", threshold));
        return Pass(nameof(BeLessThanOrEqualTo), ("threshold", threshold));
    }

    /// <summary>
    /// Asserts that the value falls within the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public ValueAsserter<T> BeInRange(T min, T max)
    {
        if (_value is not IComparable<T> c || c.CompareTo(min) < 0 || c.CompareTo(max) > 0)
            return Fail(nameof(BeInRange), $"expected in [{V(min)}, {V(max)}], was {V(_value)}", ("min", min), ("max", max));
        return Pass(nameof(BeInRange), ("min", min), ("max", max));
    }

    /// <summary>
    /// Asserts that the value falls outside the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public ValueAsserter<T> NotBeInRange(T min, T max)
    {
        if (_value is IComparable<T> c && c.CompareTo(min) >= 0 && c.CompareTo(max) <= 0)
            return Fail(nameof(NotBeInRange), $"expected outside [{V(min)}, {V(max)}], was {V(_value)}", ("min", min), ("max", max));
        return Pass(nameof(NotBeInRange), ("min", min), ("max", max));
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
            return Fail(nameof(HaveCount), $"expected {expected} element(s), was {count}", ("expected", expected));
        return Pass(nameof(HaveCount), ("expected", expected));
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
