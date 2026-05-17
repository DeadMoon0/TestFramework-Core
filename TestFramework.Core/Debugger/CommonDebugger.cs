using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Xunit.Abstractions;

namespace TestFramework.Core.Debugger;

internal static class CommonDebugger
{
    private const string RUN_DEBUGGER_PIPED_TYPE = "TestFramework.DebugUI.PipeAdapter.RunDebuggerPiped";
    private const string RUN_DEBUGGER_PIPED_PROJECT = "TestFramework.DebugUI.PipeAdapter";

    internal static IRunDebugger GetCommon()
    {
        return GetCommon(null, null);
    }

    internal static IRunDebugger GetCommon(IServiceProvider? serviceProvider, ITestOutputHelper? outputHelper)
    {
        ArrayList debuggers = [];

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

        if (CreateFromType(SearchTypeInLoadedAssemblies(RUN_DEBUGGER_PIPED_TYPE, RUN_DEBUGGER_PIPED_PROJECT)) is { } discoveredDebugger
            && !ContainsSameType(debuggers, discoveredDebugger.GetType()))
        {
            debuggers.Add(discoveredDebugger);
        }

        IRunDebugger[] debuggerArray = new IRunDebugger[debuggers.Count];
        for (int index = 0; index < debuggers.Count; index++)
            debuggerArray[index] = (IRunDebugger)debuggers[index]!;

        return CompositeRunDebugger.Create(debuggerArray);
    }

    private static bool ContainsSameInstance(ArrayList debuggers, IRunDebugger candidate)
    {
        foreach (object? debugger in debuggers)
        {
            if (ReferenceEquals(debugger, candidate))
                return true;
        }

        return false;
    }

    private static bool ContainsSameType(ArrayList debuggers, Type candidateType)
    {
        foreach (object? debugger in debuggers)
        {
            if (debugger?.GetType() == candidateType)
                return true;
        }

        return false;
    }

    private static IRunDebugger? CreateFromType(Type? type)
    {
        if (type is { } nnType)
        {
            try
            {
                return (IRunDebugger)(Activator.CreateInstance(nnType) ?? throw new InvalidOperationException("Could not create Instance of Type: " + RUN_DEBUGGER_PIPED_TYPE));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
        }
        else return null;
    }

    private static Type? SearchTypeInLoadedAssemblies(string typeName, string projName)
    {
        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            var asm = Assembly.Load(projName);
            var foundType = asm.GetType(typeName);
            if (foundType != null)
                return foundType;
        }
        catch { }

        return null;
    }
}