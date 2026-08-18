using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// How this protocol is written and read, and how to read one typed fact back as text.
/// </summary>
/// <remarks>
/// Every fact on this protocol is a JSON token, because a count that arrives as a number can be aligned,
/// compared and charted. A reader that only wants to print one still needs the three obvious cases settled
/// the same way everywhere: a string without its quotes, a null as nothing, and anything structured as
/// compact JSON.
/// </remarks>
public static class DebugJson
{
    /// <summary>
    /// The settings every envelope, payload and sidecar is written with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enums travel as their names. A journal is a file people open — to answer "what happened in this run",
    /// to grep for a step that timed out, to check what a bug report actually contains — and
    /// <c>"State": 4</c> answers none of those without a copy of this assembly's source beside it. A name also
    /// survives a member being inserted into the middle of an enum, which a number does not.
    /// </para>
    /// <para>
    /// One instance, shared, because the pipe frame and the journal line are the same bytes: a reader
    /// configured differently from the writer is a bug that only shows up on the values nobody tested.
    /// </para>
    /// </remarks>
    public static JsonSerializerSettings Settings { get; } = new()
    {
        Converters = { new StringEnumConverter() }
    };

    /// <summary>The same configuration, for the calls that take a serializer rather than settings.</summary>
    public static JsonSerializer Serializer { get; } = JsonSerializer.CreateDefault(Settings);

    /// <summary>One value as text.</summary>
    public static string Text(JToken? value) => value switch
    {
        null => string.Empty,
        JValue { Value: null } => string.Empty,
        JValue single => Convert.ToString(single.Value, CultureInfo.CurrentCulture) ?? string.Empty,
        _ => value.ToString(Formatting.None)
    };
}
