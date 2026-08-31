using System;
using System.Text;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Where a run puts the evidence it produced.
/// </summary>
/// <remarks>
/// <para>
/// A screenshot, the markup behind it, a container's log, the configuration a site was served with:
/// things that explain a run to a person and that no variable should hold, because they are files and
/// because they are about a moment rather than about a value.
/// </para>
/// <para>
/// One channel for all of them, and it is the run's rather than any package's. Each pack used to keep
/// its own folder and its own naming, which meant the evidence existed but nothing could find it: the
/// browser pack wrote screenshots into a directory named after a timestamp and a fresh identifier,
/// sharing no key with the run that produced them.
/// </para>
/// <para>
/// The file is written whether or not anything is watching. It lives in the run's own output, where a
/// person can open it and a build publishes it as an artifact — the debugger is told about it only
/// when there is a debugger to tell.
/// </para>
/// </remarks>
public sealed class WidgetStore
{
    /// <summary>How much of a text widget travels with the description.</summary>
    /// <remarks>
    /// Enough to recognise it by in a list without opening it, and bounded because this rides a message
    /// sent per widget. The whole of it is in the file, which is the point.
    /// </remarks>
    private const int PreviewBudget = 2_000;

    private readonly DebuggingRunSession session;

    internal WidgetStore(DebuggingRunSession session)
    {
        this.session = session;
    }

    /// <summary>
    /// Records a piece of evidence, writing it into the run's output.
    /// </summary>
    /// <param name="widget">What was produced.</param>
    /// <returns>
    /// Where it was written, or <see langword="null"/> when it could not be. A caller that names the
    /// file in a failure message needs the answer; one that only wanted it recorded can ignore it.
    /// </returns>
    /// <remarks>
    /// Nothing here throws. Evidence is what a run gathers about a problem, and a run that failed to
    /// take a screenshot has one problem rather than two — the same rule the value files follow.
    /// </remarks>
    public DebugValueBody? Publish(Widget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);

        byte[] bytes = widget.Bytes ?? Encoding.UTF8.GetBytes(widget.Text ?? string.Empty);

        DebugValueBody? body = session.WidgetFiles.Write(
            widget.Name,
            new DebugValueContent(widget.Form, widget.Text, widget.Bytes, bytes.LongLength));

        if (body is null)
            return null;

        session.PublishWidget(widget.Kind, widget.Name, widget.Component, Describe(widget, body, bytes.LongLength));

        return body;
    }

    /// <summary>
    /// States what the widget is, in the same terms a value is stated in.
    /// </summary>
    /// <remarks>
    /// The preview never carries a picture. Text is worth a glance in a list and bytes are not, so an
    /// image describes itself and points at its file — which is also why a widget always has a body,
    /// where a value has one only when it was too big to carry.
    /// </remarks>
    private static DebugValueDescription Describe(Widget widget, DebugValueBody body, long size)
    {
        bool readable = widget.Form is DebugPreviewForm.Text or DebugPreviewForm.Json or DebugPreviewForm.Markup;

        string text = readable ? widget.Text ?? string.Empty : string.Empty;
        bool cut = text.Length > PreviewBudget;

        return new DebugValueDescription
        {
            Summary = widget.Summary is { Length: > 0 } stated ? stated : widget.Name,
            Shape = readable ? DebugValueShape.Text : DebugValueShape.Binary,
            Badges = widget.Badges,
            Fields = widget.Fields,
            Preview = new DebugValuePreview
            {
                Form = widget.Form,
                Text = cut ? text[..PreviewBudget] : text,

                // An image says it is cut whatever its size: what travels is nothing at all, and a
                // consumer offering to open the file is the only honest thing to do with it.
                IsTruncated = cut || !readable,
                SizeInBytes = size
            },
            Body = body
        };
    }
}

/// <summary>
/// A piece of evidence, as its producer hands it over.
/// </summary>
/// <remarks>
/// The content and what it is; where in the run it came from is not stated here, because the run knows
/// that better than the producer does and states it itself.
/// </remarks>
public sealed record Widget
{
    /// <summary>Gets what sort of widget this is, from the <see cref="WidgetKinds"/> family.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets what this is of, in the producer's words: <c>after-login</c>, <c>nginx.conf</c>.
    /// </summary>
    /// <remarks>
    /// Also the file's name, so it is worth being a name rather than a sentence. Two widgets sharing
    /// one become versions of it, in the order they were produced.
    /// </remarks>
    public required string Name { get; init; }

    /// <summary>Gets how the content should be read, which decides how it is written and drawn.</summary>
    public required DebugPreviewForm Form { get; init; }

    /// <summary>Gets the content, for a widget that is bytes.</summary>
    public byte[]? Bytes { get; init; }

    /// <summary>Gets the content, for a widget that is text.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the one line to show where there is room for only one; the name is used if unset.</summary>
    public string? Summary { get; init; }

    /// <summary>Gets the named facts worth stating beside it, such as the page it was taken of.</summary>
    public DebugValueField[] Fields { get; init; } = [];

    /// <summary>Gets the short labels worth showing beside it.</summary>
    public string[] Badges { get; init; } = [];

    /// <summary>
    /// Gets the environment component that produced it, when it was not produced inside a step.
    /// </summary>
    /// <remarks>
    /// The one piece of attribution a producer states for itself, because the run cannot know it: every
    /// component is built during the same step, so without this their evidence is indistinguishable.
    /// </remarks>
    public string? Component { get; init; }
}
