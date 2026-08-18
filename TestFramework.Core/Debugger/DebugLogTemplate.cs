using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Renders a log entry's template with its values, for a reader that wants the sentence.
/// </summary>
/// <remarks>
/// <para>
/// Offered here rather than left to each consumer so that the console, the debugger's own panels and anything
/// written against this protocol read an entry the same way. It is a service to renderers, not a step on the
/// way to the wire: nothing this produces is ever transported.
/// </para>
/// <para>
/// Numbered holes are filled by the framework's own composite formatting, so a format specifier a test author
/// wrote — <c>{0:N2}</c>, <c>{1:HH:mm}</c> — still means what they meant by it. Named holes are substituted as
/// they stand. A template whose holes do not match its fields is returned as it is: showing the sentence with
/// an unfilled hole in it tells a reader more than swallowing the entry does.
/// </para>
/// </remarks>
public static class DebugLogTemplate
{
    /// <summary>Renders an entry.</summary>
    public static string Render(DebugLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return Render(entry.Template, entry.Fields);
    }

    /// <summary>Renders a template with the fields that belong to it.</summary>
    public static string Render(string template, DebugLogField[] fields)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Length == 0 || template.Length == 0)
            return template;

        return IsNumbered(fields) ? Numbered(template, fields) : Named(template, fields);
    }

    /// <summary>
    /// One field as text.
    /// </summary>
    /// <remarks>
    /// A string arrives without its quotes and an object as compact JSON. This is also what a column showing
    /// one value uses, which is why it is public: a caller displaying the fields instead of the sentence should
    /// not have to reinvent the same three cases.
    /// </remarks>
    public static string Text(DebugLogField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return DebugJson.Text(field.Value);
    }

    private static bool IsNumbered(DebugLogField[] fields)
    {
        foreach (DebugLogField field in fields)
        {
            if (!int.TryParse(field.Name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return false;
        }

        return true;
    }

    private static string Numbered(string template, DebugLogField[] fields)
    {
        object?[] values = new object?[fields.Length];

        for (int index = 0; index < fields.Length; index++)
            values[index] = fields[index].Simple ?? Text(fields[index]);

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, values);
        }
        catch (FormatException)
        {
            // A message that happens to contain a brace — a fragment of JSON, a C# snippet — is not a format
            // string, and refusing to show it would lose the one thing the entry was for.
            return template;
        }
    }

    private static string Named(string template, DebugLogField[] fields)
    {
        string rendered = template;

        foreach (DebugLogField field in fields)
            rendered = rendered.Replace("{" + field.Name + "}", Text(field), StringComparison.Ordinal);

        return rendered;
    }
}
