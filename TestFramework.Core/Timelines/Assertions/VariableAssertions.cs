using System;
using System.Collections;
using System.Collections.Generic;
using TestFramework.Core.Timelines.Assertions;

namespace TestFramework.Core.Timelines;

/// <summary>
/// The variable assertions that only make sense for some types: text, order and counting.
/// </summary>
/// <remarks>
/// <para>
/// The same split as <see cref="ValueAssertions"/>, and each checks first that the variable was set, as every
/// value assertion on <see cref="VariableAsserter{T}"/> does.
/// </para>
/// <para>
/// The extensions sit in the namespace every test already imports for <see cref="Timeline"/>, not beside the
/// asserters: an extension method needs its namespace imported, and a test that reaches <c>Should()</c> through a run
/// handle has no reason to import the assertions namespace - it would find <c>Be</c> and not find <c>Contain</c>.
/// </para>
/// </remarks>
public static class VariableAssertions
{
    /// <summary>
    /// Asserts that the variable's text contains the specified substring; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="substring">The substring that must be present.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> Contain<T>(this VariableAsserter<T> asserter, string substring) where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(Contain))?.Contain(substring);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's text does not contain the specified substring; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="substring">The substring that must not be present.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> NotContain<T>(this VariableAsserter<T> asserter, string substring) where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(NotContain))?.NotContain(substring);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's text starts with the specified prefix; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="prefix">The required prefix.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> StartWith<T>(this VariableAsserter<T> asserter, string prefix) where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(StartWith))?.StartWith(prefix);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's text ends with the specified suffix; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="suffix">The required suffix.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> EndWith<T>(this VariableAsserter<T> asserter, string suffix) where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(EndWith))?.EndWith(suffix);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's text is empty or null; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> BeEmpty<T>(this VariableAsserter<T> asserter) where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(BeEmpty))?.BeEmpty();
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's text is neither empty nor null; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">Text: a string, or any other sequence of characters.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> NotBeEmpty<T>(this VariableAsserter<T> asserter) where T : IEnumerable<char>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(NotBeEmpty))?.NotBeEmpty();
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's value is greater than the threshold; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The lower exclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> BeGreaterThan<T>(this VariableAsserter<T> asserter, T threshold) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(BeGreaterThan))?.BeGreaterThan(threshold);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's value is greater than or equal to the threshold; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The lower inclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> BeGreaterThanOrEqualTo<T>(this VariableAsserter<T> asserter, T threshold) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(BeGreaterThanOrEqualTo))?.BeGreaterThanOrEqualTo(threshold);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's value is less than the threshold; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The upper exclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> BeLessThan<T>(this VariableAsserter<T> asserter, T threshold) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(BeLessThan))?.BeLessThan(threshold);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's value is less than or equal to the threshold; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="threshold">The upper inclusive bound.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> BeLessThanOrEqualTo<T>(this VariableAsserter<T> asserter, T threshold) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(BeLessThanOrEqualTo))?.BeLessThanOrEqualTo(threshold);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's value falls within the inclusive range; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> BeInRange<T>(this VariableAsserter<T> asserter, T min, T max) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(BeInRange))?.BeInRange(min, max);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's value falls outside the inclusive range; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A type with an order.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> NotBeInRange<T>(this VariableAsserter<T> asserter, T min, T max) where T : IComparable<T>?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(NotBeInRange))?.NotBeInRange(min, max);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's collection has the expected number of items; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A collection type.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <param name="expected">The expected item count.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> HaveCount<T>(this VariableAsserter<T> asserter, int expected) where T : IEnumerable?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(HaveCount))?.HaveCount(expected);
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's collection contains no items; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A collection type.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> HaveNoItems<T>(this VariableAsserter<T> asserter) where T : IEnumerable?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(HaveNoItems))?.HaveNoItems();
        return asserter;
    }

    /// <summary>
    /// Asserts that the variable's collection contains at least one item; a variable that was never set fails.
    /// </summary>
    /// <typeparam name="T">A collection type.</typeparam>
    /// <param name="asserter">The assertion chain.</param>
    /// <returns>The assertion chain.</returns>
    public static VariableAsserter<T> HaveItems<T>(this VariableAsserter<T> asserter) where T : IEnumerable?
    {
        ArgumentNullException.ThrowIfNull(asserter);
        asserter.ValueOrFail(nameof(HaveItems))?.HaveItems();
        return asserter;
    }
}
