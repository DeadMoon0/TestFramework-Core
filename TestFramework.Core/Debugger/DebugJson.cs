using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Reads a typed fact back as text.
/// </summary>
/// <remarks>
/// Every fact on this protocol is a JSON token, because a count that arrives as a number can be aligned,
/// compared and charted. A reader that only wants to print one still needs the three obvious cases settled
/// the same way everywhere: a string without its quotes, a null as nothing, and anything structured as
/// compact JSON.
/// </remarks>
public static class DebugJson
{
    /// <summary>One value as text.</summary>
    public static string Text(JToken? value) => value switch
    {
        null => string.Empty,
        JValue { Value: null } => string.Empty,
        JValue single => Convert.ToString(single.Value, CultureInfo.CurrentCulture) ?? string.Empty,
        _ => value.ToString(Formatting.None)
    };
}
