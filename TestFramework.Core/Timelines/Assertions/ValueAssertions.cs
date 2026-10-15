using System;
using System.Collections;
using System.Collections.Generic;
using TestFramework.Core.Timelines.Assertions;
using System.Linq;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Timelines;

/// <summary>
/// The assertions that only make sense for some types: text, order and counting.
/// </summary>
/// <remarks>
/// <para>
/// Each is constrained to the types it means something for, so misuse is a compile error. They used to be members
/// of every <see cref="ValueAsserter{T}"/>: a list asked whether it contained "3" checked its type name, and an order
/// asked of a type with no order failed as if the values had simply been wrong.
/// </para>
/// <para>
/// The extensions sit in the namespace every test already imports for <see cref="Timeline"/>, not beside the
/// asserters: an extension method needs its namespace imported, and a test that reaches <c>Should()</c> through a run
/// handle has no reason to import the assertions namespace - it would find <c>Be</c> and not find <c>Contain</c>.
/// </para>
/// </remarks>
public static class ValueAssertions
{
    private static string F(object? value) => VariableFormatter.Format(value);

    // ── Text ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the text contains the specified substring.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="substring">The substring that must be present.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> Contain<T>(this ValueAsserter<T> asserter, string substring)
        where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Text(asserter.Value).Contains(substring, StringComparison.Ordinal)
            ? asserter.Pass(nameof(Contain), ("substring", substring))
            : asserter.Fail(nameof(Contain), $"{F(asserter.Value)} does not contain \"{substring}\"", ("substring", substring));
    }

    /// <summary>
    /// Asserts that the text does not contain the specified substring.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="substring">The substring that must not be present.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> NotContain<T>(this ValueAsserter<T> asserter, string substring)
        where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Text(asserter.Value).Contains(substring, StringComparison.Ordinal)
            ? asserter.Fail(nameof(NotContain), $"{F(asserter.Value)} contains \"{substring}\"", ("substring", substring))
            : asserter.Pass(nameof(NotContain), ("substring", substring));
    }

    /// <summary>
    /// Asserts that the text starts with the specified prefix.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="prefix">The required prefix.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> StartWith<T>(this ValueAsserter<T> asserter, string prefix)
        where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Text(asserter.Value).StartsWith(prefix, StringComparison.Ordinal)
            ? asserter.Pass(nameof(StartWith), ("prefix", prefix))
            : asserter.Fail(nameof(StartWith), $"{F(asserter.Value)} does not start with \"{prefix}\"", ("prefix", prefix));
    }

    /// <summary>
    /// Asserts that the text ends with the specified suffix.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="suffix">The required suffix.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> EndWith<T>(this ValueAsserter<T> asserter, string suffix)
        where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Text(asserter.Value).EndsWith(suffix, StringComparison.Ordinal)
            ? asserter.Pass(nameof(EndWith), ("suffix", suffix))
            : asserter.Fail(nameof(EndWith), $"{F(asserter.Value)} does not end with \"{suffix}\"", ("suffix", suffix));
    }

    /// <summary>
    /// Asserts that the text is empty or null.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> BeEmpty<T>(this ValueAsserter<T> asserter)
        where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Text(asserter.Value).Length == 0
            ? asserter.Pass(nameof(BeEmpty))
            : asserter.Fail(nameof(BeEmpty), $"was {F(asserter.Value)}");
    }

    /// <summary>
    /// Asserts that the text is neither empty nor null.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> NotBeEmpty<T>(this ValueAsserter<T> asserter)
        where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Text(asserter.Value).Length == 0
            ? asserter.Fail(nameof(NotBeEmpty), "was empty")
            : asserter.Pass(nameof(NotBeEmpty));
    }

    /// <summary>The characters as text: a string as itself, any other sequence of characters joined, null as empty.</summary>
    private static string Text(IEnumerable<char>? characters) => characters switch
    {
        null => "",
        string text => text,
        _ => string.Concat(characters),
    };

    // ── Order ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the value is greater than the provided threshold.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The lower exclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> BeGreaterThan<T>(this ValueAsserter<T> asserter, T threshold) where T : IComparable<T>?
        => Compare(asserter, nameof(BeGreaterThan), ">", threshold, static c => c > 0);

    /// <summary>
    /// Asserts that the value is greater than or equal to the provided threshold.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The lower inclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> BeGreaterThanOrEqualTo<T>(this ValueAsserter<T> asserter, T threshold) where T : IComparable<T>?
        => Compare(asserter, nameof(BeGreaterThanOrEqualTo), ">=", threshold, static c => c >= 0);

    /// <summary>
    /// Asserts that the value is less than the provided threshold.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The upper exclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> BeLessThan<T>(this ValueAsserter<T> asserter, T threshold) where T : IComparable<T>?
        => Compare(asserter, nameof(BeLessThan), "<", threshold, static c => c < 0);

    /// <summary>
    /// Asserts that the value is less than or equal to the provided threshold.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The upper inclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> BeLessThanOrEqualTo<T>(this ValueAsserter<T> asserter, T threshold) where T : IComparable<T>?
        => Compare(asserter, nameof(BeLessThanOrEqualTo), "<=", threshold, static c => c <= 0);

    /// <summary>
    /// Asserts that the value falls within the inclusive range.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> BeInRange<T>(this ValueAsserter<T> asserter, T min, T max) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return asserter.Value is { } value && value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0
            ? asserter.Pass(nameof(BeInRange), ("min", min), ("max", max))
            : asserter.Fail(nameof(BeInRange), $"expected in [{F(min)}, {F(max)}], was {F(asserter.Value)}", ("min", min), ("max", max));
    }

    /// <summary>
    /// Asserts that the value falls outside the inclusive range.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> NotBeInRange<T>(this ValueAsserter<T> asserter, T min, T max) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return asserter.Value is { } value && value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0
            ? asserter.Fail(nameof(NotBeInRange), $"expected outside [{F(min)}, {F(max)}], was {F(asserter.Value)}", ("min", min), ("max", max))
            : asserter.Pass(nameof(NotBeInRange), ("min", min), ("max", max));
    }

    private static ValueAsserter<T> Compare<T>(ValueAsserter<T> asserter, string name, string relation, T threshold, Func<int, bool> holds)
        where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return asserter.Value is { } value && holds(value.CompareTo(threshold))
            ? asserter.Pass(name, ("threshold", threshold))
            : asserter.Fail(name, $"expected {relation} {F(threshold)}, was {F(asserter.Value)}", ("threshold", threshold));
    }

    // ── Counting ────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the collection has the expected number of items.
    /// </summary>
    /// <typeparam name="T">A collection type.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="expected">The expected item count.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> HaveCount<T>(this ValueAsserter<T> asserter, int expected) where T : IEnumerable?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Count(asserter.Value) switch
        {
            null => asserter.Fail(nameof(HaveCount), "was null", ("expected", expected)),
            int count when count != expected => asserter.Fail(nameof(HaveCount), $"expected {expected} element(s), was {count}", ("expected", expected)),
            _ => asserter.Pass(nameof(HaveCount), ("expected", expected)),
        };
    }

    /// <summary>
    /// Asserts that the collection contains no items.
    /// </summary>
    /// <typeparam name="T">A collection type.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> HaveNoItems<T>(this ValueAsserter<T> asserter) where T : IEnumerable?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Count(asserter.Value) switch
        {
            null => asserter.Fail(nameof(HaveNoItems), "was null"),
            0 => asserter.Pass(nameof(HaveNoItems)),
            int count => asserter.Fail(nameof(HaveNoItems), $"expected empty, had {count} element(s)"),
        };
    }

    /// <summary>
    /// Asserts that the collection contains at least one item.
    /// </summary>
    /// <typeparam name="T">A collection type.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static ValueAsserter<T> HaveItems<T>(this ValueAsserter<T> asserter) where T : IEnumerable?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        return Count(asserter.Value) switch
        {
            null => asserter.Fail(nameof(HaveItems), "was null"),
            0 => asserter.Fail(nameof(HaveItems), "was empty"),
            _ => asserter.Pass(nameof(HaveItems)),
        };
    }

    private static int? Count(IEnumerable? items) => items switch
    {
        null => null,
        ICollection collection => collection.Count,
        _ => items.Cast<object>().Count(),
    };
}
