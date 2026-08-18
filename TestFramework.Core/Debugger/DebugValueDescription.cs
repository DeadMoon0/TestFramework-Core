using System;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// What a value is, stated as facts rather than as a formatted line.
/// </summary>
/// <remarks>
/// <para>
/// This replaces handing consumers a single pre-rendered string. A string decides the presentation
/// inside the producer — where the separators go, what gets cut, how long is too long — and no
/// consumer can undo any of it: a rail that wants columns, a log that wants alignment and an
/// inspector that wants the whole thing all receive the same truncated sentence.
/// </para>
/// <para>
/// So the producer states what it knows and the consumer decides how it looks. The one concession
/// to presentation is <see cref="Summary"/>, because every consumer needs a single line to fall
/// back to and picking it is the producer's job — it is the only party that knows which fact
/// matters most for that kind of value.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record DebugValueDescription
{
    /// <summary>A description of nothing, used where a value has not been observed.</summary>
    public static DebugValueDescription Empty { get; } = new() { Summary = string.Empty };

    /// <summary>
    /// Gets the one line to show where there is room for only one.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets what shape of value this is, which is what a consumer picks a renderer by.
    /// </summary>
    /// <remarks>
    /// Defaulted to <see cref="DebugValueShape.Unknown"/> rather than required, so a value replayed
    /// from a journal recorded before shapes existed still describes itself as far as it can.
    /// </remarks>
    public DebugValueShape Shape { get; init; } = DebugValueShape.Unknown;

    /// <summary>
    /// Gets the named facts about the value, in the order they are worth reading.
    /// </summary>
    /// <remarks>
    /// Ordered rather than a dictionary: which fact leads is part of what the producer is saying,
    /// and a consumer laying them out in columns needs a stable order to lay out.
    /// </remarks>
    public DebugValueField[] Fields { get; init; } = [];

    /// <summary>
    /// Gets the short labels worth showing beside the value, such as a lifecycle state.
    /// </summary>
    public string[] Badges { get; init; } = [];

    /// <summary>Gets the value's own content, when there is something worth previewing.</summary>
    public DebugValuePreview? Preview { get; init; }

    /// <summary>
    /// Gets where the whole value was written, when it was too big to carry.
    /// </summary>
    /// <remarks>
    /// Present exactly when <see cref="DebugValuePreview.IsTruncated"/> is set — that is, precisely
    /// when there is more to see than was sent. A consumer showing a truncated value can therefore
    /// always offer the rest, and the run's output can name the file instead of printing four
    /// thousand characters of it.
    /// </remarks>
    public DebugValueBody? Body { get; init; }
}

/// <summary>
/// A value that was written to a file because it was too big to send.
/// </summary>
/// <remarks>
/// <para>
/// The file lives in the run's own output rather than anywhere debugger-private, which is what makes
/// one mechanism serve three readers: a person can open it, a build publishes the folder as
/// artifacts, and a UI on the same machine reads it directly instead of asking for it back over a
/// transport.
/// </para>
/// <para>
/// This is a reference, not the value. Nothing in the debug protocol grows with the size of what a
/// run assigns.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record DebugValueBody
{
    /// <summary>Gets the full path to the file holding the value.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the path relative to the run's output folder, such as <c>values/orderId.json</c>.
    /// </summary>
    /// <remarks>
    /// What a build artifact and a log line should say. The full path is meaningless to anyone
    /// reading the results on a different machine from the one that ran them.
    /// </remarks>
    public required string RelativePath { get; init; }

    /// <summary>Gets the size of the file in bytes.</summary>
    public required long SizeInBytes { get; init; }

    /// <summary>
    /// Gets the hash of the content, which is also what decides whether a rewrite is a new version.
    /// </summary>
    public required string ContentHash { get; init; }
}

/// <summary>One named fact about a value.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record DebugValueField
{
    /// <summary>Gets the fact's name, such as <c>reference</c> or <c>length</c>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the fact, typed as it stands.
    /// </summary>
    /// <remarks>
    /// A count arrives as a number and a name as a string, so a consumer can align a column of counts or
    /// compare one against another run. Rendered text cannot be turned back into either.
    /// </remarks>
    public required JToken Value { get; init; }

    /// <summary>Gets the fact as text, for a consumer that only wants to print it.</summary>
    public string Text => DebugJson.Text(Value);

    /// <summary>
    /// Gets a value indicating whether the text was cut to fit.
    /// </summary>
    /// <remarks>
    /// Stated rather than implied by an ellipsis, so a consumer can say "5 more" or offer the rest
    /// instead of leaving the reader unsure whether the value really ends in three dots.
    /// </remarks>
    public bool IsTruncated { get; init; }
}

/// <summary>How a value's own content should be read.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum DebugPreviewForm
{
    /// <summary>There is nothing worth previewing.</summary>
    None,

    /// <summary>Plain text.</summary>
    Text,

    /// <summary>JSON, which a consumer may render as a tree.</summary>
    Json,

    /// <summary>Bytes, which a consumer may render as hex.</summary>
    Binary
}

/// <summary>
/// A look at a value's content, bounded so that carrying it costs a known amount.
/// </summary>
/// <remarks>
/// The preview is deliberately not the value. It is what fits in a message that is sent on every
/// update; the whole thing is fetched separately by whoever actually opens it.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record DebugValuePreview
{
    /// <summary>Gets how the content should be read.</summary>
    public required DebugPreviewForm Form { get; init; }

    /// <summary>Gets the content, up to the preview budget.</summary>
    public required string Text { get; init; }

    /// <summary>Gets a value indicating whether there is more content than is shown here.</summary>
    public bool IsTruncated { get; init; }

    /// <summary>Gets the full size of the content, when it is known.</summary>
    public long? SizeInBytes { get; init; }
}
