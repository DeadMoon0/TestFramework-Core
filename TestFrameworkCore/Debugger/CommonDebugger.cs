using System;
using System.Diagnostics;
using System.Reflection;

namespace TestFrameworkCore.Debugger;

internal static class CommonDebugger
{
    private const string RUN_DEBUGGER_PIPED_TYPE = "TestFrameworkDebugPipeAdapter.RunDebuggerPiped";
    private const string RUN_DEBUGGER_PIPED_PROJECT = "TestFrameworkDebugPipeAdapter";

    internal static IRunDebugger GetCommon()
    {
        if (CreateFromType(SearchTypeInLoadedAssemblies(RUN_DEBUGGER_PIPED_TYPE, RUN_DEBUGGER_PIPED_PROJECT)) is { } debugger) return debugger;
        return EmptyRunDebugger.CreateNew();
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