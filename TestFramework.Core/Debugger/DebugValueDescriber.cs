using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Turns a value into facts a consumer can lay out, and a token that says whether it changed.
/// </summary>
/// <remarks>
/// <para>
/// Both come out of <em>one</em> serialisation. The previous arrangement walked every value three
/// times per write — once to hash it for change detection, once to format a display line, once to
/// build a JSON payload — and did so whether or not anything was listening. Serialising a large
/// response body three times per assignment is a cost paid by every run, including the ones with no
/// debugger attached at all.
/// </para>
/// <para>
/// The hash is taken over the untruncated form, which is the whole point of it: two different values
/// sharing a 117-character prefix must not look identical, or the second write is silently dropped
/// on exactly the large payloads someone is most likely to be inspecting.
/// </para>
/// </remarks>
internal static class DebugValueDescriber
{
    /// <summary>How much of a rendered fact is carried before it is cut.</summary>
    internal const int FieldBudget = 120;

    /// <summary>How much of a value's content is carried in the preview on every update.</summary>
    /// <remarks>
    /// The preview rides along with every value update, so it is bounded by what is reasonable to
    /// send constantly rather than by what is reasonable to read. Anything larger is fetched by
    /// whoever actually opens the value.
    /// </remarks>
    internal const int PreviewBudget = 4_000;

    private const int MaxDepth = 8;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MaxDepth = MaxDepth
    };

    /// <summary>
    /// Describes a value and produces its change token in one pass.
    /// </summary>
    internal static DescribedValue Describe(object? value)
    {
        string serialised = Serialise(value);
        (DebugValuePreview? preview, DebugValueContent? full) = ContentOf(value, serialised);

        return new DescribedValue(
            new DebugValueDescription
            {
                Summary = Summarise(value),
                Shape = ShapeOf(value),
                Fields = [.. FieldsOf(value)],
                Preview = preview
            },
            Token(serialised),
            full);
    }

    /// <summary>
    /// The whole of a value, for a caller that has already decided it needs to write it down.
    /// </summary>
    /// <remarks>
    /// Serialises again, which is the price of describing an artifact through a public entry point
    /// that returns only a description. It is paid only for artifacts whose content did not fit in a
    /// preview, and only while something is capturing.
    /// </remarks>
    internal static DebugValueContent? FullContentOf(object? value) => ContentOf(value, Serialise(value)).Full;

    /// <summary>
    /// Describes a value without producing a change token, for callers that already know it moved.
    /// </summary>
    /// <remarks>
    /// An artifact reports on its own lifecycle transitions rather than on content changing, so it
    /// has no use for a token and should not pay for one.
    /// </remarks>
    internal static DebugValueDescription DescribeForArtifact(object? value)
        => new()
        {
            Summary = Summarise(value),
            Shape = ShapeOf(value),
            Fields = [.. FieldsOf(value)],
            Preview = ContentOf(value, Serialise(value)).Preview
        };

    /// <summary>One line describing a value, for a caller that only wants the summary.</summary>
    internal static string Line(object? value) => Summarise(value);

    /// <summary>
    /// The one line a consumer falls back to.
    /// </summary>
    /// <remarks>
    /// Says what the value <em>is</em> rather than reciting as much of it as fits: "[412 items]"
    /// tells a reader more about a large collection than its first four entries do, and the entries
    /// are available in the preview for anyone who wants them.
    /// </remarks>
    private static string Summarise(object? value) => value switch
    {
        null => "<null>",
        string text => $"\"{Cut(OneLine(text), FieldBudget).Text}\"",
        bool flag => flag ? "True" : "False",
        byte[] bytes => $"{bytes.Length} bytes",
        IDictionary dictionary => $"{{{dictionary.Count} entries}}",
        ICollection collection => $"[{collection.Count} items]",
        IEnumerable sequence => $"[{Count(sequence)} items]",
        _ => HasOwnToString(value.GetType())
            ? Cut(value.ToString() ?? "<null>", FieldBudget).Text
            : value.GetType().Name
    };

    /// <summary>
    /// Which of the handful of shapes a value has.
    /// </summary>
    /// <remarks>
    /// The order of the cases is the whole content of this method: a string is an
    /// <see cref="IEnumerable"/> of characters and a <see cref="byte"/> array is an
    /// <see cref="ICollection"/>, so both have to be caught before the sequence cases claim them.
    /// </remarks>
    internal static DebugValueShape ShapeOf(object? value) => value switch
    {
        null => DebugValueShape.Null,
        string => DebugValueShape.Text,
        byte[] => DebugValueShape.Binary,
        IDictionary => DebugValueShape.Dictionary,
        IEnumerable => DebugValueShape.Collection,
        _ => IsScalar(value) ? DebugValueShape.Scalar : DebugValueShape.Object
    };

    /// <summary>The named facts worth stating about a value's shape.</summary>
    private static IEnumerable<DebugValueField> FieldsOf(object? value)
    {
        if (value is null)
            yield break;

        yield return Fact("type", TypeNameOf(value.GetType()));

        switch (value)
        {
            case string text:
                yield return Fact("length", text.Length);
                break;

            case byte[] bytes:
                // Named for its unit and carried as a number. "4096 bytes" was a fact and its unit
                // welded together, which a consumer could neither sum nor sort.
                yield return Fact("bytes", bytes.Length);
                break;

            case IDictionary dictionary:
                yield return Fact("entries", dictionary.Count);
                break;

            case ICollection collection:
                yield return Fact("items", collection.Count);
                break;

            case IEnumerable sequence:
                yield return Fact("items", Count(sequence));
                break;
        }
    }

    /// <summary>
    /// A bounded look at the content, and the whole of it for a caller that means to write it down.
    /// </summary>
    /// <remarks>
    /// Both come out of the same branch, because deciding what form a value takes — text, JSON, bytes
    /// — is a decision that must not be made twice and differently. The serialised form is reused
    /// rather than recomputed: it is the same text the change token was taken over, and walking the
    /// object again would reintroduce exactly the cost this class exists to remove.
    /// </remarks>
    private static (DebugValuePreview? Preview, DebugValueContent? Full) ContentOf(object? value, string serialised)
    {
        if (value is null)
            return (null, null);

        if (value is byte[] bytes)
        {
            (string hex, bool cut) = Cut(Convert.ToHexString(bytes), PreviewBudget);

            return (
                new DebugValuePreview
                {
                    Form = DebugPreviewForm.Binary,
                    Text = hex,
                    IsTruncated = cut,
                    SizeInBytes = bytes.Length
                },
                new DebugValueContent(DebugPreviewForm.Binary, null, bytes, bytes.LongLength));
        }

        if (value is string text)
        {
            (string shown, bool cut) = Cut(text, PreviewBudget);
            long size = Encoding.UTF8.GetByteCount(text);

            return (
                new DebugValuePreview
                {
                    Form = DebugPreviewForm.Text,
                    Text = shown,
                    IsTruncated = cut,
                    SizeInBytes = size
                },
                new DebugValueContent(DebugPreviewForm.Text, text, null, size));
        }

        if (IsScalar(value))
            return (null, null);

        (string json, bool trimmed) = Cut(serialised, PreviewBudget);
        long jsonSize = Encoding.UTF8.GetByteCount(serialised);

        return (
            new DebugValuePreview
            {
                Form = DebugPreviewForm.Json,
                Text = json,
                IsTruncated = trimmed,
                SizeInBytes = jsonSize
            },
            new DebugValueContent(DebugPreviewForm.Json, serialised, null, jsonSize));
    }

    /// <summary>
    /// Serialises a value once, tolerating values that cannot be serialised at all.
    /// </summary>
    /// <remarks>
    /// A debug payload that throws must not take the run down with it, and it must still produce
    /// <em>something</em> distinct enough to detect a change with — a constant here would make every
    /// unserialisable value look unchanged forever.
    /// </remarks>
    private static string Serialise(object? value)
    {
        if (value is null)
            return "<null>";

        try
        {
            return JsonConvert.SerializeObject(value, JsonSettings);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            return $"<unserializable {value.GetType().FullName}: {value}>";
        }
    }

    private static string Token(string serialised)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(serialised)));

    private static DebugValueField Fact(string name, int count)
        => new() { Name = name, Value = new JValue(count) };

    private static DebugValueField Fact(string name, string value)
        => new() { Name = name, Value = new JValue(value) };

    private static (string Text, bool WasCut) Cut(string value, int budget)
        => value.Length <= budget ? (value, false) : (value[..budget], true);

    /// <summary>
    /// Flattens line breaks and runs of whitespace, so a one-line summary really is one line.
    /// </summary>
    /// <remarks>
    /// The summary is what a consumer shows where it has room for exactly one line. A multi-line
    /// string put through it unchanged does not get truncated by the layout — it wraps, and one
    /// value's summary takes seven rows of a rail sized for one. The full text is in the preview and
    /// the file, both of which keep their line breaks.
    /// </remarks>
    private static string OneLine(string value)
    {
        if (value.AsSpan().IndexOfAny('\r', '\n', '\t') < 0)
            return value;

        StringBuilder flattened = new(value.Length);
        bool lastWasSpace = false;

        foreach (char character in value)
        {
            bool isSpace = char.IsWhiteSpace(character);

            if (isSpace && lastWasSpace)
                continue;

            flattened.Append(isSpace ? ' ' : character);
            lastWasSpace = isSpace;
        }

        return flattened.ToString();
    }

    private static int Count(IEnumerable sequence)
    {
        int count = 0;

        foreach (object? _ in sequence)
            count++;

        return count;
    }

    /// <summary>Whether a value is small enough that a preview would only repeat its summary.</summary>
    private static bool IsScalar(object value)
        => value is bool or char or DateTime or DateTimeOffset or TimeSpan or Guid or Uri
           || value.GetType().IsPrimitive
           || value is decimal
           || value.GetType().IsEnum;

    private static bool HasOwnToString(Type type)
        => type.GetMethod("ToString", Type.EmptyTypes)!.DeclaringType != typeof(object);

    /// <summary>A readable type name, with generic arguments spelled out.</summary>
    private static string TypeNameOf(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        string name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeNameOf))}>";
    }
}

/// <summary>A described value, the token that says whether it changed, and the whole of its content.</summary>
/// <remarks>
/// The content rides along because the caller is the only party that knows the value's key, and the
/// key is what a file has to be named after. Re-deriving it in the caller would mean serialising the
/// value a second time.
/// </remarks>
internal readonly record struct DescribedValue(DebugValueDescription Description, string ChangeToken, DebugValueContent? Content);
