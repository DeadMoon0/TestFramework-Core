using System;
using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// One piece of evidence a run produced, and where in the run it came from.
/// </summary>
/// <remarks>
/// <para>
/// A run says what happened — steps, values, checks, log lines — but not what it <em>looked like</em>.
/// A browser test that walked four pages and a queue test that moved three messages both come out as
/// a row of named boxes. A widget is the missing half: a file the run produced, described, and
/// attributed to the step and attempt that produced it.
/// </para>
/// <para>
/// A run concept rather than any one package's. The alternative was each pack writing evidence into a
/// folder of its own and a tool knowing where to look, which is a private arrangement between two
/// packages that a third cannot join — and it is what the UI pack did before this existed.
/// </para>
/// <para>
/// <see cref="Kind"/> is what a consumer picks a renderer by, in the same way a value's shape is. It
/// is a string rather than an enum precisely so a package can add one without Core shipping first:
/// a consumer that does not recognise a kind still has a description, a summary and a file it can
/// offer to open.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record DebugWidgetEntry
{
    /// <summary>Gets the stage the widget was produced in, when it was produced inside a step.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the step's index within its stage.</summary>
    public int? StepId { get; init; }

    /// <summary>
    /// Gets which attempt of the step produced it, counting from one.
    /// </summary>
    /// <remarks>
    /// A step that retries produces evidence per attempt, and the interesting one is usually not the
    /// last. This is the only signal besides a log entry that carries the attempt, because it is the
    /// only other one where two of them can differ.
    /// </remarks>
    public int? Attempt { get; init; }

    /// <summary>
    /// Gets the environment component that produced it, when it was not a step.
    /// </summary>
    /// <remarks>
    /// A container's log and a site's generated configuration are produced while the environment is
    /// being built, which is one step for the whole of it. Naming the component is what tells four
    /// widgets filed against that step apart.
    /// </remarks>
    public string? Component { get; init; }

    /// <summary>Gets what sort of widget this is, from the <c>tf.widget.*</c> family.</summary>
    public required string Kind { get; init; }

    /// <summary>Gets what this widget is of, in the producer's words: <c>after-login</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Gets when it was taken.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Gets what the widget is, stated the same way a value is.
    /// </summary>
    /// <remarks>
    /// Reusing the value description rather than inventing a second shape is what makes a widget
    /// travel through everything already built for values: it is collected into a shared bundle,
    /// resolved on the machine that opens it, and compared against an earlier run by content hash,
    /// none of which needed a line of new code.
    /// <para>
    /// One difference from a value, and it is worth stating: a widget's
    /// <see cref="DebugValueDescription.Body"/> is always present, because a widget <em>is</em> a
    /// file. A value's body appears only when the value was too big to carry.
    /// </para>
    /// </remarks>
    public required DebugValueDescription Description { get; init; }
}

/// <summary>
/// The kinds of widget the family produces.
/// </summary>
/// <remarks>
/// Stable strings, in the same family as <see cref="DebugValueSchemaKeys"/> and for the same reason: a
/// package builds against a published Core, so it carries the literal and a test of its own pins that
/// the literal still matches this. Adding a kind here is not what lets a package emit one.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class WidgetKinds
{
    /// <summary>A picture of what something looked like: a browser page, a rendered document.</summary>
    public const string Screenshot = "tf.widget.screenshot";

    /// <summary>A document the run produced or was configured by: markup, a config file, a script.</summary>
    public const string Document = "tf.widget.document";

    /// <summary>Output a process or a container produced, as a stream of lines.</summary>
    public const string LogStream = "tf.widget.logstream";
}
