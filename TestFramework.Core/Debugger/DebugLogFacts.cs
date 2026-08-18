using System;
using System.Collections.Generic;
using System.Globalization;

namespace TestFramework.Core.Debugger;

/// <summary>
/// What a log event has to say, before anybody decides how it should look.
/// </summary>
/// <remarks>
/// <para>
/// A template and the values that fill it. The template is the sentence with holes in it, kept whole rather
/// than filled in, because a consumer that has both can render the sentence, ignore it and show the values as
/// columns, or group a hundred entries by the fact that they share a template.
/// </para>
/// <para>
/// An event that returns no facts at all is narrating the console — a rule of box-drawing characters, a padded
/// table of stages — and is not carried on the debug transport. The transport already states those facts as
/// signals, and shipping the sentence about them as well is shipping the same thing twice.
/// </para>
/// </remarks>
public sealed record DebugLogFacts
{
    /// <summary>Gets the sentence, with its holes unfilled.</summary>
    public required string Template { get; init; }

    /// <summary>Gets the values that fill the holes.</summary>
    public DebugLogField[] Fields { get; init; } = [];

    /// <summary>States a template and its named values.</summary>
    public static DebugLogFacts Of(string template, params (string Name, object? Value)[] fields)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(fields);

        List<DebugLogField> named = new(fields.Length);

        foreach ((string name, object? value) in fields)
            named.Add(DebugLogField.Of(name, value));

        return new DebugLogFacts { Template = template, Fields = [.. named] };
    }

    /// <summary>
    /// States a composite format string and its arguments.
    /// </summary>
    /// <remarks>
    /// The holes are numbered, because that is what <see cref="string.Format(string, object[])"/> takes and
    /// what the logging methods have always accepted. Rendering them is then the framework's own formatting
    /// with the arguments it was given, rather than a re-parse of a sentence somebody already assembled.
    /// </remarks>
    public static DebugLogFacts Positional(string format, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(arguments);

        DebugLogField[] fields = new DebugLogField[arguments.Length];

        for (int index = 0; index < arguments.Length; index++)
            fields[index] = DebugLogField.Of(index.ToString(CultureInfo.InvariantCulture), arguments[index]);

        return new DebugLogFacts { Template = format, Fields = fields };
    }
}
