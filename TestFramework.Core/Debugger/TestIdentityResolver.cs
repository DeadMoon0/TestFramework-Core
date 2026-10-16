using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace TestFramework.Core.Debugger;

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

    /// <summary>
    /// The project path is a property of the process, so it cannot change between runs. Resolving it
    /// scans every loaded assembly; doing that once per process instead of once per run is free.
    /// </summary>
    private static readonly Lazy<string> CachedProjectPath = new(ResolveProjectPath, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Resolves the identity of the test on the current stack, run from the assembly this process is testing.
    /// </summary>
    internal static TestIdentity Resolve(string? sourceFilePath, int sourceLineNumber)
        => Resolve(CachedProjectPath.Value, sourceFilePath, sourceLineNumber);

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

            RuntimeMethodHandle handle;
            try
            {
                handle = method.MethodHandle;
            }
            catch (InvalidOperationException)
            {
                // A Reflection.Emit method has no runtime handle and throws when asked for one.
                // xunit invokes theory methods through exactly such frames, and they sit between
                // the resolver and the test method — never the test method itself, so walk on.
                continue;
            }

            ResolvedMethod resolved = MethodCache.GetOrAdd(handle, _ => Describe(method));
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
            // The attribute's own type and everything it derives from. Deriving from a test attribute
            // is ordinary practice rather than an exotic case — a suite that skips itself when a
            // browser is missing writes `BrowserFact : FactAttribute`, and xUnit's own theory
            // attribute is a subclass too. Matching the exact name alone made every test in such a
            // suite unidentifiable: the run reported the host process instead of the test, which
            // silently cost it its breakpoints, its baseline comparison and its re-run filter.
            for (Type? type = attribute.GetType(); type is not null; type = type.BaseType)
            {
                foreach ((string candidate, TestFrameworkKind framework) in TestAttributes)
                {
                    if (string.Equals(type.Name, candidate, StringComparison.Ordinal))
                        return framework;
                }
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

    private static string ResolveProjectPath()
    {
        string? processPath = System.Environment.ProcessPath;
        if (LooksLikeTestHostPath(processPath))
            return processPath!;

        string? commandLineAssembly = System.Environment.GetCommandLineArgs().FirstOrDefault(LooksLikeUserAssemblyPath);
        if (!string.IsNullOrWhiteSpace(commandLineAssembly))
            return commandLineAssembly;

        string? loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(assembly => assembly.Location)
            .FirstOrDefault(LooksLikeUserAssemblyPath);
        if (!string.IsNullOrWhiteSpace(loadedAssembly))
            return loadedAssembly;

        if (!string.IsNullOrWhiteSpace(processPath))
            return processPath;

        return Assembly.GetEntryAssembly()?.Location
            ?? processPath
            ?? Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
    }

    private static bool LooksLikeUserAssemblyPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return false;

        string fileName = Path.GetFileName(path);
        if (fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
            return false;

        return !fileName.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            && !fileName.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTestHostPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && Path.GetFileName(path).StartsWith("testhost", StringComparison.OrdinalIgnoreCase);
    }
}
