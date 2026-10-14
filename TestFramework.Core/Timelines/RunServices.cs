using System;

namespace TestFramework.Core.Timelines;

/// <summary>
/// Services handed to a run together with the responsibility for disposing them.
/// </summary>
/// <remarks>
/// <para>
/// A service provider passed to <c>SetupRun(IServiceProvider)</c> stays the caller's: it may serve several runs, so
/// the run never disposes it. A provider built for one run and handed over has no other owner, and nothing
/// disposed it - Config's <c>SetupRun(ConfigInstance)</c> built one per run and every singleton it registered
/// outlived the run.
/// </para>
/// <para>
/// A type rather than a flag, so the ownership is visible at the call that hands it over and cannot be passed by
/// accident: a plain provider overload would quietly start disposing providers callers reuse across runs. It is
/// itself a service provider and goes through the ordinary <c>SetupRun</c> overloads, because overloads of their own
/// made the common <c>SetupRun(null, output.WriteLine)</c> ambiguous.
/// </para>
/// </remarks>
public sealed class RunServices : IServiceProvider
{
    private RunServices(IServiceProvider provider)
    {
        Provider = provider;
    }

    /// <summary>The services the run uses.</summary>
    internal IServiceProvider Provider { get; }

    /// <inheritdoc />
    public object? GetService(Type serviceType) => Provider.GetService(serviceType);

    /// <summary>
    /// Services the run owns: disposed when the run has finished, whether it passed or not.
    /// </summary>
    /// <remarks>
    /// A run builder that is never run disposes nothing - build the provider when you are about to run.
    /// </remarks>
    /// <param name="provider">The provider; disposed with the run if it is disposable.</param>
    public static RunServices Owned(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new RunServices(provider);
    }
}
