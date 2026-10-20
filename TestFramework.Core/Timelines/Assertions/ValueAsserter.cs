using TestFramework.Core.Variables;
using System;
using System.Linq;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Timelines.Assertions;

/// <summary>
/// Provides fluent assertions for a concrete value.
/// </summary>
/// <remarks>
/// Only what holds for every type lives here. Assertions that only make sense for some types - text, order,
/// counting - are extension methods in <see cref="ValueAssertions"/>, constrained to those types, so asking a list
/// whether it starts with "a" does not compile instead of quietly checking the list's type name.
/// </remarks>
/// <typeparam name="T">The asserted value type.</typeparam>
public class ValueAsserter<T>
{
    private readonly T _value;

    /// <summary>The value asserted on, for the type-specific assertions in <see cref="ValueAssertions"/>.</summary>
    internal T Value => _value;
    private readonly string _expression;
    private readonly ScopedLogger? _logger;
    private readonly bool _secret;

    /// <summary>What a check is reported against - by default the expression itself.</summary>
    /// <remarks>
    /// Apart from the expression the failure message reads, because a variable's value checks come through here:
    /// they are reported as checks on that variable, by its bare name, so a consumer can tie them to it.
    /// </remarks>
    private readonly DebugAssertionTargetKind _targetKind;
    private readonly string _target;

    internal ValueAsserter(T value, string expression, ScopedLogger? logger)
        : this(value, expression, logger, false)
    {
    }

    internal ValueAsserter(T value, string expression, ScopedLogger? logger, bool secret)
        : this(value, expression, logger, secret, DebugAssertionTargetKind.Value, expression)
    {
    }

    internal ValueAsserter(T value, string expression, ScopedLogger? logger, bool secret, DebugAssertionTargetKind targetKind, string target)
    {
        _secret = secret;
        _value = value;
        _expression = expression;
        _logger = logger;
        _targetKind = targetKind;
        _target = target;
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

    internal ValueAsserter<T> Pass(string assertionName, params (string Name, object? Value)[] arguments)
    {
        _logger?.SignalAssertion(_targetKind, _target, assertionName, Shown(arguments), true, Shown());
        return this;
    }

    // A secret is compared like any other value; what the assertion reports about it - the actual value, the
    // expected one the test wrote, and the reason that would quote them - is redacted.
    private object? Shown() => _secret ? VariableStore.Secret : _value;

    private (string Name, object? Value)[] Shown((string Name, object? Value)[] arguments)
        => _secret ? [.. arguments.Select(static argument => (argument.Name, (object?)VariableStore.Secret))] : arguments;

    internal ValueAsserter<T> Fail(string assertionName, string reason, params (string Name, object? Value)[] arguments)
    {
        _logger?.SignalAssertion(_targetKind, _target, assertionName, Shown(arguments), false, Shown());

        // The thrown message is for whoever is reading a failed test, so it is rendered here - from the same
        // name and arguments the transport carries, rather than from a second string built beside them.
        string shownReason = _secret ? "the value is secret, so it is not shown" : reason;
        var message = $"{_expression}: {Call(assertionName, Shown(arguments))} failed \u2014 {shownReason}";
        AssertionFailure.Raise(_logger, message, static (text, site) => new ValueAssertionException(text, site));
        return this;
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
    public ValueAsserter<T> Match(Func<T, bool> predicate) => this.Match(predicate, "custom predicate");

    /// <summary>
    /// Asserts that the value satisfies the provided predicate, saying what it checks.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="true"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public ValueAsserter<T> Match(Func<T, bool> predicate, string description)
    {
        if (!predicate(_value))
            return Fail(nameof(Match), $"value {V(_value)} did not satisfy: {description}", ("description", description));
        return Pass(nameof(Match), ("description", description));
    }

    /// <summary>
    /// Asserts that the value does not satisfy the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    public ValueAsserter<T> NotMatch(Func<T, bool> predicate) => this.NotMatch(predicate, "custom predicate");

    /// <summary>
    /// Asserts that the value does not satisfy the provided predicate, saying what it checks.
    /// </summary>
    /// <param name="predicate">The predicate that must return <see langword="false"/>.</param>
    /// <param name="description">A short description included in assertion output.</param>
    public ValueAsserter<T> NotMatch(Func<T, bool> predicate, string description)
    {
        if (predicate(_value))
            return Fail(nameof(NotMatch), $"value {V(_value)} unexpectedly satisfied: {description}", ("description", description));
        return Pass(nameof(NotMatch), ("description", description));
    }

    // ── Chaining ────────────────────────────────────────────────────────────

    /// <summary>
    /// Continues the fluent assertion chain.
    /// </summary>
    public ValueAsserter<T> And() => this;
}
