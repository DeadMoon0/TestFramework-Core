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
        List<IRunDebugger> debuggers = [];

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

        IRunDebugger builtInPipeDebugger = new PipeRunDebugger();
        if (!ContainsSameType(debuggers, builtInPipeDebugger.GetType()))
        {
            debuggers.Add(builtInPipeDebugger);
        }

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
}