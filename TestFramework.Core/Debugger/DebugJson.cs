using System;
using System.Globalization;
using System.Linq;
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

    /// <summary>
    /// One value as text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An object with a single property reads as that property's value. Framework messages are full of
    /// one-property wrappers — an app identifier, an HTTP method, a queue name — and their structure is worth
    /// carrying while their JSON is not worth reading: <c>FunctionApp '{"Identifier":"func"}'</c> is a worse
    /// sentence than <c>FunctionApp 'func'</c>, and the wrapper is what the reader means by the value.
    /// </para>
    /// <para>
    /// An object with more than one property keeps its JSON. There is no single value to unwrap to, and picking
    /// one would be inventing an answer.
    /// </para>
    /// </remarks>
    public static string Text(JToken? value) => value switch
    {
        null => string.Empty,
        JValue { Value: null } => string.Empty,
        JValue single => Convert.ToString(single.Value, CultureInfo.CurrentCulture) ?? string.Empty,
        JObject { Count: 1 } wrapper => Text(wrapper.Properties().First().Value),
        _ => value.ToString(Formatting.None)
    };
}
