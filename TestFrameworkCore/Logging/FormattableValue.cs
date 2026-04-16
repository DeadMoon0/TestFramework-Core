using System.Collections.Generic;

namespace TestFrameworkCore.Logging;

/// <summary>
/// Carries a typed value that compares by its underlying data and renders via the
/// framework's variable formatter when converted to a string.
///
/// This lets you pass values around and defer formatting until output time —
/// collections show item counts and previews, strings are quoted and truncated,
/// objects are serialised as JSON where no meaningful ToString exists.
///
/// Use it wherever you want consistent, human-readable display without giving up
/// proper typed equality.
/// </summary>
public readonly struct FormattableValue<T> : System.IEquatable<FormattableValue<T>>
{
    /// <summary>The underlying raw value. Used for equality and comparisons.</summary>
    public T Raw { get; }

    public FormattableValue(T value) => Raw = value;

    /// <summary>Implicitly wraps any <typeparamref name="T"/> value.</summary>
    public static implicit operator FormattableValue<T>(T value) => new(value);

    /// <summary>Implicitly unwraps back to <typeparamref name="T"/>.</summary>
    public static implicit operator T(FormattableValue<T> fv) => fv.Raw;

    /// <summary>
    /// Formats the wrapped value using the framework's variable formatter —
    /// the same formatter used in test logs for variables and artifacts.
    /// </summary>
    public override string ToString() => VariableFormatter.Format(Raw);

    public bool Equals(FormattableValue<T> other) =>
        EqualityComparer<T>.Default.Equals(Raw, other.Raw);

    public override bool Equals(object? obj) =>
        obj is FormattableValue<T> other && Equals(other);

    public override int GetHashCode() => Raw?.GetHashCode() ?? 0;

    public static bool operator ==(FormattableValue<T> left, FormattableValue<T> right) => left.Equals(right);
    public static bool operator !=(FormattableValue<T> left, FormattableValue<T> right) => !left.Equals(right);
}
