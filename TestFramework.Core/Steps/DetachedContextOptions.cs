using System;
using System.Threading;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Steps;

/// <summary>
/// How <see cref="RunContext.Detached(DetachedContextOptions)"/> sets up a context that belongs to no run.
/// </summary>
/// <remarks>
/// A record of init properties rather than four optional parameters: those four were each "or nothing", and the
/// next one added as a parameter would have broken every package compiled against the old signature.
/// </remarks>
public sealed record DetachedContextOptions
{
    /// <summary>Gets the services the step should resolve from, or <see langword="null"/> for none.</summary>
    public IServiceProvider? Services { get; init; }

    /// <summary>
    /// Gets something to watch what the step does, when the test is about that - the same socket a run debugger
    /// plugs into, so a package can assert on what its own step reported. <see langword="null"/> watches nothing.
    /// </summary>
    public IRunDebugger? Debugger { get; init; }

    /// <summary>Gets what stops the work; nothing does by default.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Gets how long the step has, or <see langword="null"/> for no deadline.</summary>
    public TimeSpan? Timeout { get; init; }
}
