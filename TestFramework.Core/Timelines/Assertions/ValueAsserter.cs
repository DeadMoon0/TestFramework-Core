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

    private ValueAsserter<T> Pass(string assertionName, string assertionDisplay, string expected = "", string actual = "")
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Value, _expression, assertionName, assertionDisplay, true, expected, actual);
        return this;
    }

    private ValueAsserter<T> Fail(string assertionName, string assertionDisplay, string reason, string expected = "", string actual = "")
    {
        _logger?.SignalAssertion(DebugAssertionTargetKind.Value, _expression, assertionName, assertionDisplay, false, expected, actual, reason);
        var message = $"{_expression}: {assertionDisplay} failed \u2014 {reason}";
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
            return Fail(nameof(BeNull), nameof(BeNull), $"was {V(_value)}", "null", Render(_value));
        return Pass(nameof(BeNull), nameof(BeNull), "null", Render(_value));
    }

    /// <summary>
    /// Asserts that the value is not <see langword="null"/>.
    /// </summary>
    public ValueAsserter<T> NotBeNull()
    {
        if (_value is null)
            return Fail(nameof(NotBeNull), nameof(NotBeNull), "was null", "not null", Render(_value));
        return Pass(nameof(NotBeNull), nameof(NotBeNull), "not null", Render(_value));
    }

    // ── Equality ────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the value equals the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    public ValueAsserter<T> Be(T expected)
    {
        if (!Equals(_value, expected))
            return Fail(nameof(Be), $"{nameof(Be)}({V(expected)})", $"expected {V(expected)}, was {V(_value)}", Render(expected), Render(_value));
        return Pass(nameof(Be), $"{nameof(Be)}({V(expected)})", Render(expected), Render(_value));
    }

    /// <summary>
    /// Asserts that the value does not equal the expected value.
    /// </summary>
    /// <param name="expected">The value that must not match.</param>
    public ValueAsserter<T> NotBe(T expected)
    {
        if (Equals(_value, expected))
            return Fail(nameof(NotBe), $"{nameof(NotBe)}({V(expected)})", $"expected not {V(expected)}", $"not {Render(expected)}", Render(_value));
        return Pass(nameof(NotBe), $"{nameof(NotBe)}({V(expected)})", $"not {Render(expected)}", Render(_value));
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
            return Fail(nameof(Match), $"{nameof(Match)}({description})", $"value {V(_value)} did not satisfy: {description}", description, Render(_value));
        return Pass(nameof(Match), $"{nameof(Match)}({description})", description, Render(_value));
    }

    /// <summary>
    /// Asserts that the value does not satisfy the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public ValueAsserter<T> NotMatch(Func<T, bool> predicate, string description = "custom predicate")
    {
        if (predicate(_value))
            return Fail(nameof(NotMatch), $"{nameof(NotMatch)}({description})", $"value {V(_value)} unexpectedly satisfied: {description}", $"not {description}", Render(_value));
        return Pass(nameof(NotMatch), $"{nameof(NotMatch)}({description})", $"not {description}", Render(_value));
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
            return Fail(nameof(Contain), $"{nameof(Contain)}(\"{substring}\")", $"{V(_value)} does not contain \"{substring}\"", substring, s);
        return Pass(nameof(Contain), $"{nameof(Contain)}(\"{substring}\")", substring, s);
    }

    /// <summary>
    /// Asserts that the string representation does not contain the specified substring.
    /// </summary>
    /// <param name="substring">The substring that must not be present.</param>
    public ValueAsserter<T> NotContain(string substring)
    {
        var s = _value?.ToString() ?? "";
        if (s.Contains(substring))
            return Fail(nameof(NotContain), $"{nameof(NotContain)}(\"{substring}\")", $"{V(_value)} contains \"{substring}\"", $"not contain {substring}", s);
        return Pass(nameof(NotContain), $"{nameof(NotContain)}(\"{substring}\")", $"not contain {substring}", s);
    }

    /// <summary>
    /// Asserts that the string representation starts with the specified prefix.
    /// </summary>
    /// <param name="prefix">The required prefix.</param>
    public ValueAsserter<T> StartWith(string prefix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.StartsWith(prefix, StringComparison.Ordinal))
            return Fail(nameof(StartWith), $"{nameof(StartWith)}(\"{prefix}\")", $"{V(_value)} does not start with \"{prefix}\"", prefix, s);
        return Pass(nameof(StartWith), $"{nameof(StartWith)}(\"{prefix}\")", prefix, s);
    }

    /// <summary>
    /// Asserts that the string representation ends with the specified suffix.
    /// </summary>
    /// <param name="suffix">The required suffix.</param>
    public ValueAsserter<T> EndWith(string suffix)
    {
        var s = _value?.ToString() ?? "";
        if (!s.EndsWith(suffix, StringComparison.Ordinal))
            return Fail(nameof(EndWith), $"{nameof(EndWith)}(\"{suffix}\")", $"{V(_value)} does not end with \"{suffix}\"", suffix, s);
        return Pass(nameof(EndWith), $"{nameof(EndWith)}(\"{suffix}\")", suffix, s);
    }

    /// <summary>
    /// Asserts that the string representation is empty.
    /// </summary>
    public ValueAsserter<T> BeEmpty()
    {
        var s = _value?.ToString() ?? "";
        if (s.Length != 0)
            return Fail(nameof(BeEmpty), nameof(BeEmpty), $"was {V(_value)}", "empty", s);
        return Pass(nameof(BeEmpty), nameof(BeEmpty), "empty", s);
    }

    /// <summary>
    /// Asserts that the string representation is not empty.
    /// </summary>
    public ValueAsserter<T> NotBeEmpty()
    {
        var s = _value?.ToString() ?? "";
        if (s.Length == 0)
            return Fail(nameof(NotBeEmpty), nameof(NotBeEmpty), "was empty", "not empty", s);
        return Pass(nameof(NotBeEmpty), nameof(NotBeEmpty), "not empty", s);
    }

    // ── Numeric / Comparable ────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the value is greater than the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower exclusive bound.</param>
    public ValueAsserter<T> BeGreaterThan(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) <= 0)
            return Fail(nameof(BeGreaterThan), $"{nameof(BeGreaterThan)}({V(threshold)})", $"expected > {V(threshold)}, was {V(_value)}", $"> {Render(threshold)}", Render(_value));
        return Pass(nameof(BeGreaterThan), $"{nameof(BeGreaterThan)}({V(threshold)})", $"> {Render(threshold)}", Render(_value));
    }

    /// <summary>
    /// Asserts that the value is greater than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The lower inclusive bound.</param>
    public ValueAsserter<T> BeGreaterThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) < 0)
            return Fail(nameof(BeGreaterThanOrEqualTo), $"{nameof(BeGreaterThanOrEqualTo)}({V(threshold)})", $"expected >= {V(threshold)}, was {V(_value)}", $">= {Render(threshold)}", Render(_value));
        return Pass(nameof(BeGreaterThanOrEqualTo), $"{nameof(BeGreaterThanOrEqualTo)}({V(threshold)})", $">= {Render(threshold)}", Render(_value));
    }

    /// <summary>
    /// Asserts that the value is less than the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper exclusive bound.</param>
    public ValueAsserter<T> BeLessThan(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) >= 0)
            return Fail(nameof(BeLessThan), $"{nameof(BeLessThan)}({V(threshold)})", $"expected < {V(threshold)}, was {V(_value)}", $"< {Render(threshold)}", Render(_value));
        return Pass(nameof(BeLessThan), $"{nameof(BeLessThan)}({V(threshold)})", $"< {Render(threshold)}", Render(_value));
    }

    /// <summary>
    /// Asserts that the value is less than or equal to the provided threshold.
    /// </summary>
    /// <param name="threshold">The upper inclusive bound.</param>
    public ValueAsserter<T> BeLessThanOrEqualTo(T threshold)
    {
        if (_value is not IComparable<T> c || c.CompareTo(threshold) > 0)
            return Fail(nameof(BeLessThanOrEqualTo), $"{nameof(BeLessThanOrEqualTo)}({V(threshold)})", $"expected <= {V(threshold)}, was {V(_value)}", $"<= {Render(threshold)}", Render(_value));
        return Pass(nameof(BeLessThanOrEqualTo), $"{nameof(BeLessThanOrEqualTo)}({V(threshold)})", $"<= {Render(threshold)}", Render(_value));
    }

    /// <summary>
    /// Asserts that the value falls within the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public ValueAsserter<T> BeInRange(T min, T max)
    {
        if (_value is not IComparable<T> c || c.CompareTo(min) < 0 || c.CompareTo(max) > 0)
            return Fail(nameof(BeInRange), $"{nameof(BeInRange)}([{V(min)}, {V(max)}])", $"expected in [{V(min)}, {V(max)}], was {V(_value)}", $"[{Render(min)}, {Render(max)}]", Render(_value));
        return Pass(nameof(BeInRange), $"{nameof(BeInRange)}([{V(min)}, {V(max)}])", $"[{Render(min)}, {Render(max)}]", Render(_value));
    }

    /// <summary>
    /// Asserts that the value falls outside the inclusive range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    public ValueAsserter<T> NotBeInRange(T min, T max)
    {
        if (_value is IComparable<T> c && c.CompareTo(min) >= 0 && c.CompareTo(max) <= 0)
            return Fail(nameof(NotBeInRange), $"{nameof(NotBeInRange)}([{V(min)}, {V(max)}])", $"expected outside [{V(min)}, {V(max)}], was {V(_value)}", $"outside [{Render(min)}, {Render(max)}]", Render(_value));
        return Pass(nameof(NotBeInRange), $"{nameof(NotBeInRange)}([{V(min)}, {V(max)}])", $"outside [{Render(min)}, {Render(max)}]", Render(_value));
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
            return Fail(nameof(HaveCount), $"{nameof(HaveCount)}({expected})", $"expected {expected} element(s), was {count}", expected.ToString(), count.ToString());
        return Pass(nameof(HaveCount), $"{nameof(HaveCount)}({expected})", expected.ToString(), count.ToString());
    }

    /// <summary>
    /// Asserts that the collection contains no items.
    /// </summary>
    public ValueAsserter<T> HaveNoItems()
    {
        int count = CountItems(_value);
        if (count != 0)
            return Fail(nameof(HaveNoItems), nameof(HaveNoItems), $"expected empty, had {count} element(s)", "0 items", count.ToString());
        return Pass(nameof(HaveNoItems), nameof(HaveNoItems), "0 items", count.ToString());
    }

    /// <summary>
    /// Asserts that the collection contains at least one item.
    /// </summary>
    public ValueAsserter<T> HaveItems()
    {
        int count = CountItems(_value);
        if (count == 0)
            return Fail(nameof(HaveItems), nameof(HaveItems), "was empty", "at least 1 item", count.ToString());
        return Pass(nameof(HaveItems), nameof(HaveItems), "at least 1 item", count.ToString());
    }

    // ── Chaining ────────────────────────────────────────────────────────────

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public ValueAsserter<T> And() => this;
}
