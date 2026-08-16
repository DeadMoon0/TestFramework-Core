using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TestFramework.Core.Logging;

internal static class VariableFormatter
{
    private const int MaxDepth = 4;
    private const int MaxStringLength = 120;
    private const int MaxCollectionPreviewItems = 4;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MaxDepth = MaxDepth
    };

    internal static string Format(object? value) => value switch
    {
        null => "<NULL>",
        string s => $"\"{Truncate(s)}\"",
        bool b => b ? "True" : "False",
        IEnumerable e => FormatEnumerable(e),
        _ => HasMeaningfulToString(value.GetType())
               ? Truncate(value.ToString() ?? "<NULL>")
               : TrySerialize(value)
    };

    /// <summary>
    /// Produces a stable fingerprint of a value's full content, for detecting that it changed.
    /// </summary>
    /// <remarks>
    /// <see cref="Format"/> is a display helper and truncates at 120 characters, which makes it
    /// unusable as a change rule: two different values sharing a 117-character prefix format
    /// identically, so the second write looks like a no-op and never reaches a debugger. That is a
    /// silent loss on exactly the large payloads — request bodies, result rows — someone is most
    /// likely to be inspecting.
    /// <para>
    /// A hash rather than the text itself, because the untruncated form of a large value can be
    /// megabytes and one is retained per variable for the life of the run. Comparing the values
    /// directly is not an option either: a reference type mutated in place is still equal to
    /// itself, and that change must be reported.
    /// </para>
    /// </remarks>
    internal static string CreateChangeToken(object? value)
    {
        string full = FormatFull(value);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(full));
        return Convert.ToBase64String(hash);
    }

    private static string FormatFull(object? value)
    {
        if (value is null)
            return "<NULL>";

        try
        {
            return JsonConvert.SerializeObject(value, JsonSettings);
        }
        catch
        {
            // Unserializable values still need a token; fall back to the display form, accepting
            // that two such values sharing a truncated prefix look alike. Rare, and far narrower
            // than applying that rule to everything.
            return Format(value);
        }
    }

    private static bool HasMeaningfulToString(Type t) =>
        t.GetMethod("ToString", Type.EmptyTypes)!.DeclaringType != typeof(object);

    private static string TrySerialize(object value)
    {
        try
        {
            return Truncate(JsonConvert.SerializeObject(value, JsonSettings));
        }
        catch
        {
            return $"<{value.GetType().Name}>";
        }
    }

    private static string FormatEnumerable(IEnumerable e)
    {
        if (e is IDictionary dictionary)
            return FormatDictionary(dictionary);

        var preview = new List<string>(MaxCollectionPreviewItems);
        int count = 0;
        foreach (var item in e)
        {
            count++;
            if (preview.Count < MaxCollectionPreviewItems)
                preview.Add(FormatCollectionItem(item));
        }

        if (count == 0) return "[0 items]";

        var previewSuffix = count > MaxCollectionPreviewItems ? ", ..." : string.Empty;
        return $"[{count} items] [{string.Join(", ", preview)}{previewSuffix}]";
    }

    private static string FormatDictionary(IDictionary dictionary)
    {
        var entries = new List<string>(MaxCollectionPreviewItems);
        int count = 0;
        foreach (DictionaryEntry entry in dictionary)
        {
            count++;
            if (entries.Count < MaxCollectionPreviewItems)
            {
                string key = FormatCollectionItem(entry.Key);
                string value = FormatCollectionItem(entry.Value);
                entries.Add($"{key}: {value}");
            }
        }

        if (count == 0) return "{0 entries}";

        var previewSuffix = count > MaxCollectionPreviewItems ? ", ..." : string.Empty;
        return $"{{{count} entries}} {{{string.Join(", ", entries)}{previewSuffix}}}";
    }

    private static string FormatCollectionItem(object? value)
    {
        return value switch
        {
            null => "<NULL>",
            string s => $"\"{Truncate(s, 40)}\"",
            bool b => b ? "True" : "False",
            IEnumerable and not string => $"<{value.GetType().Name}>",
            _ => HasMeaningfulToString(value.GetType())
                ? Truncate(value.ToString() ?? "<NULL>", 60)
                : $"<{value.GetType().Name}>"
        };
    }

    private static string Truncate(string value, int maxLength = MaxStringLength)
    {
        if (value.Length <= maxLength) return value;
        return value.Substring(0, maxLength - 3) + "...";
    }
}
