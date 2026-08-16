using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

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

internal static class TestIdentityResolver
{
    /// <summary>
    /// Attribute simple names, matched textually so Core takes no dependency on any test framework.
    /// </summary>
    private static readonly (string Attribute, TestFrameworkKind Framework)[] TestAttributes =
    [
        ("FactAttribute", TestFrameworkKind.XUnit),
        ("TheoryAttribute", TestFrameworkKind.XUnit),
        ("TestAttribute", TestFrameworkKind.NUnit),
        ("TestCaseAttribute", TestFrameworkKind.NUnit),
        ("TestCaseSourceAttribute", TestFrameworkKind.NUnit),
        ("TestMethodAttribute", TestFrameworkKind.MSTest),
        ("DataTestMethodAttribute", TestFrameworkKind.MSTest)
    ];

    /// <summary>
    /// Resolution is stable per method, and a suite re-running the same test would otherwise repeat
    /// the stack walk on every run.
    /// </summary>
    private static readonly ConcurrentDictionary<RuntimeMethodHandle, ResolvedMethod> MethodCache = new();

    private static readonly ConcurrentDictionary<string, string?> ProjectFileCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record ResolvedMethod(string? TypeFullName, string? MethodName, string? AssemblyName, TestFrameworkKind Framework);

    internal static TestIdentity Resolve(string assemblyPath, string? sourceFilePath, int sourceLineNumber)
    {
        ResolvedMethod resolved = FindTestMethod();

        string? fullyQualifiedName = resolved.TypeFullName is not null && resolved.MethodName is not null
            ? $"{resolved.TypeFullName}.{resolved.MethodName}"
            : null;

        return new TestIdentity
        {
            DisplayName = resolved.MethodName ?? AppDomain.CurrentDomain.FriendlyName,
            TypeFullName = resolved.TypeFullName,
            MethodName = resolved.MethodName,
            FullyQualifiedName = fullyQualifiedName,
            Framework = resolved.Framework,
            AssemblyPath = assemblyPath,
            AssemblyName = resolved.AssemblyName,
            SourceFilePath = sourceFilePath,
            SourceLineNumber = sourceLineNumber,
            ProjectFilePath = FindProjectFile(sourceFilePath)
        };
    }

    private static ResolvedMethod FindTestMethod()
    {
        // File info is never read here — the source location comes from caller attributes, which the
        // compiler fills in for free — so the walk stays the cheap variant.
        StackFrame[] frames = new StackTrace(skipFrames: 1, fNeedFileInfo: false).GetFrames();

        foreach (StackFrame frame in frames)
        {
            if (frame.GetMethod() is not MethodInfo method)
                continue;

            ResolvedMethod resolved = MethodCache.GetOrAdd(method.MethodHandle, _ => Describe(method));
            if (resolved.Framework != TestFrameworkKind.Unknown)
                return resolved;
        }

        return new ResolvedMethod(null, null, null, TestFrameworkKind.Unknown);
    }

    private static ResolvedMethod Describe(MethodInfo method)
    {
        TestFrameworkKind framework = DetectFramework(method);
        if (framework == TestFrameworkKind.Unknown)
            return new ResolvedMethod(null, null, null, TestFrameworkKind.Unknown);

        Type? declaringType = method.DeclaringType;

        return new ResolvedMethod(
            declaringType?.FullName,
            method.Name,
            declaringType?.Assembly.GetName().Name,
            framework);
    }

    private static TestFrameworkKind DetectFramework(MethodInfo method)
    {
        object[] attributes;
        try
        {
            attributes = method.GetCustomAttributes(inherit: true);
        }
        catch (Exception e)
        {
            // A method whose attributes cannot be loaded is simply not the frame we want.
            Debug.WriteLine(e);
            return TestFrameworkKind.Unknown;
        }

        foreach (object attribute in attributes)
        {
            string name = attribute.GetType().Name;

            foreach ((string candidate, TestFrameworkKind framework) in TestAttributes)
            {
                if (string.Equals(name, candidate, StringComparison.Ordinal))
                    return framework;
            }
        }

        return TestFrameworkKind.Unknown;
    }

    /// <summary>
    /// Walks up from the source file to the nearest project file, which is what a re-run has to
    /// point <c>dotnet test</c> at.
    /// </summary>
    private static string? FindProjectFile(string? sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            return null;

        return ProjectFileCache.GetOrAdd(sourceFilePath, static path =>
        {
            try
            {
                DirectoryInfo? directory = new FileInfo(path).Directory;

                while (directory is not null)
                {
                    string? project = Directory
                        .EnumerateFiles(directory.FullName, "*.csproj")
                        .OrderBy(file => file, StringComparer.Ordinal)
                        .FirstOrDefault();

                    if (project is not null)
                        return project;

                    directory = directory.Parent;
                }
            }
            catch (Exception e)
            {
                // The source tree may not exist on this machine — a journal opened elsewhere, for
                // instance. Not knowing the project is a normal outcome, not a failure.
                Debug.WriteLine(e);
            }

            return null;
        });
    }
}
