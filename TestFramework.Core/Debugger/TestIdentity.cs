using System;
using System.IO;

namespace TestFramework.Core.Debugger;

/// <summary>
/// The test framework a run was started from.
/// </summary>
public enum TestFrameworkKind
{
    /// <summary>No test method was found on the stack, or its attributes were not recognised.</summary>
    Unknown,

    /// <summary>xUnit.net.</summary>
    XUnit,

    /// <summary>NUnit.</summary>
    NUnit,

    /// <summary>MSTest.</summary>
    MSTest
}

/// <summary>
/// Identifies the test that started a run, precisely enough to find it again and to re-run it.
/// </summary>
/// <remarks>
/// The previous naming reported only the bare method name and recognised xUnit alone, so two tests
/// with the same method name in different classes were indistinguishable and NUnit or MSTest users
/// got the process's friendly name. Nothing about that can drive a filter, which is why re-running a
/// selected test needs this first: a wrong filter runs the wrong test and then reports the result as
/// yours.
/// </remarks>
public sealed record TestIdentity
{
    /// <summary>Gets the display name for the run, falling back to something recognisable.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the declaring type's full name, or null when no test frame was found.</summary>
    public string? TypeFullName { get; init; }

    /// <summary>Gets the test method's name, or null when no test frame was found.</summary>
    public string? MethodName { get; init; }

    /// <summary>
    /// Gets the filter-ready identifier, <c>Namespace.Type.Method</c>, or null when unknown.
    /// </summary>
    public string? FullyQualifiedName { get; init; }

    /// <summary>Gets the test framework that owns the method.</summary>
    public required TestFrameworkKind Framework { get; init; }

    /// <summary>Gets the path of the assembly or host that identifies this run.</summary>
    public required string AssemblyPath { get; init; }

    /// <summary>Gets the declaring assembly's simple name, when a test frame was found.</summary>
    public string? AssemblyName { get; init; }

    /// <summary>Gets the source file the run was started from, captured at compile time.</summary>
    public string? SourceFilePath { get; init; }

    /// <summary>Gets the line the run was started from, captured at compile time.</summary>
    public int SourceLineNumber { get; init; }

    /// <summary>Gets the nearest project file above the source file, when one was found.</summary>
    public string? ProjectFilePath { get; init; }

    /// <summary>
    /// Gets what to call the project this run belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test's own project or assembly first, and <see cref="AssemblyPath"/> only as a last
    /// resort: under a test runner that path is the host process, so every run in a suite reports
    /// <c>testhost.exe</c> — which names the runner rather than the run, and is the same for all of
    /// them.
    /// </para>
    /// <para>
    /// One rule, stated once, because a consumer grouping runs by project and a log line naming the
    /// project have to agree. Two copies of this drifting apart would file a run under one name and
    /// print another.
    /// </para>
    /// </remarks>
    public string ProjectDisplayName => ProjectFilePath ?? AssemblyName ?? AssemblyPath;

    /// <summary>Gets the project's name on its own, without the directory or the extension.</summary>
    public string ProjectName => ShortNameOf(ProjectDisplayName);

    /// <summary>
    /// Reduces a project path to the name a person would call it.
    /// </summary>
    /// <remarks>
    /// Only a real project or assembly extension is trimmed. A bare assembly name is dotted too, and
    /// trimming its last segment would turn <c>Acme.Billing.Tests</c> into <c>Acme.Billing</c> — a
    /// project that does not exist, sitting beside the one that does.
    /// </remarks>
    public static string ShortNameOf(string project)
    {
        if (string.IsNullOrWhiteSpace(project))
            return project;

        string file = project[(project.LastIndexOfAny(['/', '\\']) + 1)..];

        if (file.Length == 0)
            return project;

        string suffix = Path.GetExtension(file);

        return suffix is ".csproj" or ".dll" or ".exe" ? file[..^suffix.Length] : file;
    }

    /// <summary>
    /// Gets a value indicating whether this identity is complete enough to re-run the test.
    /// </summary>
    /// <remarks>
    /// A consumer offering "re-run" must check this and say why it is unavailable rather than
    /// guessing a filter.
    /// </remarks>
    public bool CanRerun => Framework != TestFrameworkKind.Unknown
        && !string.IsNullOrEmpty(FullyQualifiedName)
        && !string.IsNullOrEmpty(ProjectFilePath);
}
