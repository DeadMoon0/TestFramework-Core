using System;
using System.Collections;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace TestFramework.Core.Debugger;

internal static class CommonDebugger
{
    internal static IRunDebugger GetCommon()
    {
        return GetCommon(null, null);
    }

    internal static IRunDebugger GetCommon(IServiceProvider? serviceProvider, ITestOutputHelper? outputHelper)
    {
        return GetCommon(serviceProvider, outputHelper, out _);
    }

    /// <summary>
    /// Builds the debugger fan-out for one run.
    /// </summary>
    /// <param name="serviceProvider">The service provider that may supply externally registered debuggers.</param>
    /// <param name="outputHelper">The xunit output helper to mirror the run into, when one is available.</param>
    /// <param name="ownedResources">
    /// Receives the debuggers this method constructed, so the caller can release them when the run
    /// ends. Debuggers resolved from the service provider are never included: their lifetime belongs
    /// to the container, not to the run.
    /// </param>
    internal static IRunDebugger GetCommon(IServiceProvider? serviceProvider, ITestOutputHelper? outputHelper, out IDisposable? ownedResources)
    {
        List<IRunDebugger> debuggers = [];
        List<IDisposable> owned = [];

        if (serviceProvider is not null)
        {
            Type enumerableType = typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType(typeof(IRunDebugger));
            if (serviceProvider.GetService(enumerableType) is IEnumerable registeredDebuggers)
            {
                foreach (object? registeredDebugger in registeredDebuggers)
                {
                    if (registeredDebugger is IRunDebugger debugger && !ContainsSameInstance(debuggers, debugger))
                        debuggers.Add(debugger);
                }
            }
        }

        if (serviceProvider?.GetService(typeof(IRunDebugger)) is IRunDebugger singleDebugger && !ContainsSameInstance(debuggers, singleDebugger))
            debuggers.Add(singleDebugger);

        if (outputHelper is not null)
            debuggers.Add(new OutputRunDebugger(outputHelper));

        // Skip the pipe debugger entirely once this process has established that nothing is
        // listening: constructing one costs a connect probe per run and buys nothing.
        if (PipeTransport.GetMode() != PipeDebuggerMode.Off
            && !PipeClient.IsKnownUnavailable(PipeTransport.GetPipeName())
            && !ContainsSameType(debuggers, typeof(PipeRunDebugger)))
        {
            PipeRunDebugger builtInPipeDebugger = new();
            debuggers.Add(builtInPipeDebugger);
            owned.Add(builtInPipeDebugger);
        }

        ownedResources = owned.Count == 0 ? null : new OwnedDebuggerResources(owned);

        return CompositeRunDebugger.Create([.. debuggers]);
    }

    private static bool ContainsSameInstance(IEnumerable<IRunDebugger> debuggers, IRunDebugger candidate)
    {
        foreach (IRunDebugger debugger in debuggers)
        {
            if (ReferenceEquals(debugger, candidate))
                return true;
        }

        return false;
    }

    private static bool ContainsSameType(IEnumerable<IRunDebugger> debuggers, Type candidateType)
    {
        foreach (IRunDebugger debugger in debuggers)
        {
            if (debugger.GetType() == candidateType)
                return true;
        }

        return false;
    }

    private sealed class OwnedDebuggerResources(IReadOnlyList<IDisposable> resources) : IDisposable
    {
        public void Dispose()
        {
            foreach (IDisposable resource in resources)
            {
                try
                {
                    resource.Dispose();
                }
                catch (Exception exception)
                {
                    // Tearing down a debug channel must never be the reason a run reports a failure.
                    System.Diagnostics.Debug.WriteLine(exception);
                }
            }
        }
    }
}
