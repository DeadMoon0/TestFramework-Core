using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// The documents' C# is code that compiles: every block is a region of the docs project, and says which.
/// </summary>
/// <remarks>
/// The packaged README called <c>run.Widgets</c>, which does not exist; the only documented custom step violated its
/// own base class's constraint and named assertions that were never written. Nothing compiled them. Here each
/// block is compared with code the build compiles, so a sample that stops compiling breaks the build and a sample
/// that drifts from the code breaks this test.
/// </remarks>
public class DocSnippetTests
{
    private static readonly Regex SnippetMarker = new(@"^<!-- snippet: (?<name>[a-z0-9-]+) -->$", RegexOptions.Compiled);
    private static readonly Regex RegionStart = new(@"^\s*#region snippet:(?<name>[a-z0-9-]+)\s*$", RegexOptions.Compiled);
    private static readonly Regex RegionEnd = new(@"^\s*#endregion\s*$", RegexOptions.Compiled);

    /// <summary>The documents whose C# is held to this, relative to the repository root.</summary>
    private static readonly string[] Documents =
    [
        "README.md",
        Path.Combine("TestFramework.Core", "README.md"),
        Path.Combine("Documentation", "Arc42.md"),
        Path.Combine("Documentation", "CoreArchitecture.md"),
    ];

    [Fact]
    public void EveryCSharpBlockNamesTheCompiledSnippetItShows()
    {
        string[] unmarked = [.. Blocks().Where(block => block.Name is null).Select(block => block.Where)];

        Assert.True(unmarked.Length == 0, $"C# blocks with no '<!-- snippet: name -->' line above them: {string.Join(", ", unmarked)}. Put the code in the docs project and name it.");
    }

    [Fact]
    public void EveryCSharpBlockIsTheCodeItsSnippetCompiles()
    {
        IReadOnlyDictionary<string, string> regions = Regions();
        List<string> drifted = [];

        foreach (DocBlock block in Blocks().Where(block => block.Name is not null))
        {
            if (!regions.TryGetValue(block.Name!, out string? region))
                drifted.Add($"'{block.Name}' ({block.Where}) has no '#region snippet:{block.Name}' in the docs project");
            else if (Normalized(region) != Normalized(block.Code))
                drifted.Add($"'{block.Name}' ({block.Where}) differs from its region");
        }

        Assert.True(drifted.Count == 0, string.Join(System.Environment.NewLine, drifted));
    }

    [Fact]
    public void EverySnippetInTheDocsProjectIsShownSomewhere()
    {
        HashSet<string> shown = [.. Blocks().Select(block => block.Name).OfType<string>()];
        string[] orphaned = [.. Regions().Keys.Where(name => !shown.Contains(name)).Order(StringComparer.Ordinal)];

        Assert.True(orphaned.Length == 0, $"Snippets no document shows: {string.Join(", ", orphaned)}");
    }

    private static IReadOnlyList<DocBlock> Blocks() => [.. Documents.SelectMany(BlocksOf)];

    private static IEnumerable<DocBlock> BlocksOf(string document)
    {
        string[] lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), document));
        List<DocBlock> blocks = [];

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() != "```csharp")
                continue;

            string? name = i > 0 && SnippetMarker.Match(lines[i - 1].Trim()) is { Success: true } marker ? marker.Groups["name"].Value : null;
            int end = Array.FindIndex(lines, i + 1, line => line.Trim() == "```");
            blocks.Add(new DocBlock(name, $"{document}:{i + 1}", string.Join('\n', lines[(i + 1)..end])));
            i = end;
        }

        return blocks;
    }

    /// <summary>Each snippet's code, its regions joined in file order.</summary>
    private static IReadOnlyDictionary<string, string> Regions()
    {
        Dictionary<string, List<string>> regions = new(StringComparer.Ordinal);
        string project = Path.Combine(RepositoryRoot(), "UnitTests", "TestFramework.Core.Docs");

        foreach (string file in Directory.GetFiles(project, "*.cs").Order(StringComparer.Ordinal))
        {
            List<string>? current = null;
            foreach (string line in File.ReadAllLines(file))
            {
                if (RegionStart.Match(line) is { Success: true } start)
                {
                    string name = start.Groups["name"].Value;
                    current = regions.TryGetValue(name, out List<string>? existing) ? existing : regions[name] = [];
                }
                else if (RegionEnd.IsMatch(line))
                {
                    current = null;
                }
                else
                {
                    current?.Add(line);
                }
            }
        }

        return regions.ToDictionary(pair => pair.Key, pair => string.Join('\n', pair.Value), StringComparer.Ordinal);
    }

    /// <summary>Code without indentation or blank lines: what a reader copies, not how either file lays it out.</summary>
    private static string Normalized(string code)
        => string.Join('\n', code.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TestFramework.Core.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException($"No TestFramework.Core.slnx above '{AppContext.BaseDirectory}'.");
    }

    private sealed record DocBlock(string? Name, string Where, string Code);
}
