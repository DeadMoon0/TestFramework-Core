using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace TestFramework.Core.Logging;

internal static class VariableFormatter
{
    private const int MaxDepth = 4;
    private const int MaxStringLength = 120;
    private const int MaxCollectionPreviewItems = 4;

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = false,
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

    private static bool HasMeaningfulToString(Type t) =>
        t.GetMethod("ToString", Type.EmptyTypes)!.DeclaringType != typeof(object);

    private static string TrySerialize(object value)
    {
        try
        {
            return Truncate(JsonSerializer.Serialize(value, _jsonOptions));
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
