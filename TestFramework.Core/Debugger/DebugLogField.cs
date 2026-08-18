using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// One named fact behind a log entry.
/// </summary>
/// <remarks>
/// <para>
/// The value is a JSON token rather than a string, so a number stays a number and a flag stays a flag. A
/// consumer can then sort by it, compare it against another run, or group by it — none of which is possible
/// once <c>42</c> has been formatted into the middle of a sentence.
/// </para>
/// <para>
/// This is the whole point of the log protocol: the framework says what happened and with which values, and
/// whoever displays it decides how that reads.
/// </para>
/// </remarks>
public sealed record DebugLogField
{
    /// <summary>
    /// How much JSON one field is allowed to contribute.
    /// </summary>
    /// <remarks>
    /// A log argument is normally a number or a short string. It can also be whatever object a test author
    /// passed, and an object graph serialised in full would travel down a named pipe and into the permanent
    /// record of every run that logged it. Past this size the field carries a description instead.
    /// </remarks>
    public const int MaximumSerializedLength = 8 * 1024;

    /// <summary>Gets the field's name, which is the hole it fills in the template.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the value, typed as it was logged.</summary>
    public required JToken Value { get; init; }

    /// <summary>
    /// Gets the value as the object it came from, where it was a simple one.
    /// </summary>
    /// <remarks>
    /// For rendering: a composite format string applied to the underlying <see cref="DateTimeOffset"/> or
    /// <see cref="double"/> honours its format specifiers, which it cannot do against a token.
    /// </remarks>
    public object? Simple => Value is JValue single ? single.Value : null;

    /// <summary>Names a value, typing it as it stands.</summary>
    public static DebugLogField Of(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new DebugLogField { Name = name, Value = Wrap(value) };
    }

    /// <summary>
    /// Turns a logged argument into a token.
    /// </summary>
    /// <remarks>
    /// Anything that is not a value type or a string is serialised as an object, which is what makes a logged
    /// request or result inspectable rather than a sentence. A type that cannot be serialised at all — one with
    /// a cycle, or a live handle — falls back to its own description: losing the structure of an awkward
    /// argument is worth more than failing the run that logged it.
    /// </remarks>
    private static JToken Wrap(object? value)
    {
        if (value is null)
            return JValue.CreateNull();

        if (value is JToken token)
            return Cap(token);

        Type type = value.GetType();

        if (type.IsPrimitive || value is string or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Enum)
            return new JValue(value);

        try
        {
            return Cap(JToken.FromObject(value));
        }
        catch (JsonException)
        {
            return new JValue(Describe(value));
        }
        catch (InvalidOperationException)
        {
            return new JValue(Describe(value));
        }
    }

    private static JToken Cap(JToken token)
    {
        if (token is JValue)
            return token;

        string json = token.ToString(Formatting.None);

        return json.Length <= MaximumSerializedLength
            ? token
            : new JValue(string.Format(
                CultureInfo.InvariantCulture,
                "[{0} of JSON, too large to carry]",
                json.Length));
    }

    private static string Describe(object value)
        => string.Format(CultureInfo.InvariantCulture, "[{0}]", value.GetType().Name);
}
