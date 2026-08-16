using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Writes values that are too big to send into the run's output, and hands back a reference.
/// </summary>
/// <remarks>
/// <para>
/// A large response body used to leave a run through two bad options: truncated to a few thousand
/// characters, which loses the part someone needed, or in full through every update, which makes the
/// debug protocol grow with whatever the run happens to assign. Writing it once and passing a path is
/// neither.
/// </para>
/// <para>
/// Everything here is best-effort. A run that cannot write to its own output directory is not a
/// failing run — it is a run whose values are merely truncated, which is exactly where it stood
/// before any of this existed.
/// </para>
/// </remarks>
internal sealed class DebugValueFileStore
{
    private const string ValuesFolderName = "values";
    private const int MaxKeyLength = 60;

    private readonly Func<string> resolveRunDirectory;
    private readonly object gate = new();

    /// <summary>
    /// What has already been written for each key: the content hash, and the reference handed out.
    /// </summary>
    /// <remarks>
    /// An artifact republishes on every lifecycle change while its data stays put, so without this a
    /// single unchanged blob is written once per transition under a new version number.
    /// </remarks>
    private readonly Dictionary<string, List<WrittenBody>> written = [];

    private string? runDirectory;
    private bool unusable;

    internal DebugValueFileStore(Func<string> resolveRunDirectory)
    {
        this.resolveRunDirectory = resolveRunDirectory;
    }

    /// <summary>
    /// Writes the content for a key, or returns the earlier file when the content has not changed.
    /// </summary>
    /// <returns>A reference to the file, or <see langword="null"/> when it could not be written.</returns>
    internal DebugValueBody? Write(string key, DebugValueContent content)
    {
        byte[] bytes = content.Bytes ?? Encoding.UTF8.GetBytes(content.Text ?? string.Empty);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));

        lock (gate)
        {
            if (unusable)
                return null;

            if (!written.TryGetValue(key, out List<WrittenBody>? history))
                written[key] = history = [];

            foreach (WrittenBody earlier in history)
            {
                if (earlier.Hash == hash)
                    return earlier.Body;
            }

            DebugValueBody? body = TryWrite(key, history.Count + 1, bytes, hash, Extension(content.Form));

            if (body is not null)
                history.Add(new WrittenBody(hash, body));

            return body;
        }
    }

    private DebugValueBody? TryWrite(string key, int version, byte[] bytes, string hash, string extension)
    {
        try
        {
            string directory = EnsureValuesDirectory();

            // The first write of a key is named plainly. Versions only appear once there is more than
            // one, so the common case reads as "orderId.json" rather than "orderId.v1.json".
            string name = version == 1
                ? $"{RunOutput.SafeName(key, MaxKeyLength)}.{extension}"
                : $"{RunOutput.SafeName(key, MaxKeyLength)}.v{version.ToString(CultureInfo.InvariantCulture)}.{extension}";

            string path = Path.Combine(directory, name);

            File.WriteAllBytes(path, bytes);

            return new DebugValueBody
            {
                Path = path,
                RelativePath = $"{ValuesFolderName}/{name}",
                SizeInBytes = bytes.LongLength,
                ContentHash = hash
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // Once the output directory has proved unwritable, every later value would fail the same
            // way. Give up on the whole store rather than throwing an exception per assignment.
            unusable = true;

            return null;
        }
    }

    private string EnsureValuesDirectory()
    {
        // Created on first use, so a run that never assigns anything large leaves no empty folders
        // behind for a build to publish.
        runDirectory ??= resolveRunDirectory();

        string values = Path.Combine(runDirectory, ValuesFolderName);

        Directory.CreateDirectory(values);

        return values;
    }

    private static string Extension(DebugPreviewForm form) => form switch
    {
        DebugPreviewForm.Json => "json",
        DebugPreviewForm.Binary => "bin",
        _ => "txt"
    };

    private readonly record struct WrittenBody(string Hash, DebugValueBody Body);
}

/// <summary>The whole of a value, in the form it should be written in.</summary>
/// <remarks>
/// Binary values carry their bytes rather than the hex the preview shows: the point of writing a file
/// is that someone can open it with the tool that understands it, and no tool understands a hex dump
/// of a PNG.
/// </remarks>
internal sealed record DebugValueContent(DebugPreviewForm Form, string? Text, byte[]? Bytes, long SizeInBytes);
