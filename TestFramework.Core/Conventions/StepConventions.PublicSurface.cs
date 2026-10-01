using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Conventions;

public static partial class StepConventions
{
    /// <summary>
    /// Nothing on the assembly's public surface has a shape that cannot grow without breaking compiled callers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every package is compiled on its own and run against whatever version of its neighbours NuGet picks. A
    /// default parameter value is baked into the <em>caller</em>, so adding one more optional parameter changes
    /// the method's signature: everything compiled against the old one still builds and then throws
    /// <see cref="MissingMethodException"/> at run time. A positional record is the same trap - its constructor is
    /// its member list. Both looked like the cheap way to add a knob, and one of them already broke 31 tests.
    /// </para>
    /// <para>
    /// So the rule is the shape, not the discipline: a knob from a small fixed set is an explicit overload, an
    /// open set is an options record with <c>init</c> properties, and a public positional record carries
    /// <see cref="ClosedShapeAttribute"/> saying why it is complete. Caller-information parameters are exempt -
    /// the compiler fills them and nobody passes them by hand.
    /// </para>
    /// </remarks>
    /// <param name="assembly">The assembly to check.</param>
    /// <returns>How many public types were checked.</returns>
    /// <exception cref="FrameworkConfigurationException">When a public member takes an optional parameter, or a
    /// public positional record is not declared closed.</exception>
    public static ConventionReport AssertPublicSurfaceCanGrow(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Type[] types = ExportedTypesOf(assembly);
        List<string> offenders = [];

        foreach (Type type in types)
        {
            offenders.AddRange(OptionalParametersOf(type));

            if (IsPositionalRecord(type) && type.GetCustomAttribute<ClosedShapeAttribute>() is null)
            {
                offenders.Add($"{Describe(type)} is a positional record without [ClosedShape]");
            }
        }

        if (offenders.Count != 0)
        {
            throw new FrameworkConfigurationException(
                $"{offenders.Count} member(s) on '{assembly.GetName().Name}''s public surface cannot grow without breaking compiled callers.")
            {
                RecoverySteps = [
                    "Replace an optional parameter from a small fixed set with explicit overloads.",
                    "Replace a set of knobs that will grow with an options record whose members are init properties.",
                    "Give a record that will grow init properties instead of a positional constructor; mark one whose members are its identity [ClosedShape(\"why\")].",
                ],
                AvailableOptions = [.. offenders.OrderBy(static offender => offender, StringComparer.Ordinal)],
            };
        }

        return new ConventionReport(types.Length, []);
    }

    internal static IEnumerable<string> OptionalParametersOf(Type type)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        IEnumerable<MethodBase> members = type.GetConstructors(declared).Cast<MethodBase>().Concat(type.GetMethods(declared));

        foreach (MethodBase member in members.Where(member => IsReachable(type, member)))
        {
            foreach (ParameterInfo parameter in member.GetParameters().Where(static parameter => parameter.IsOptional && !IsCallerInformation(parameter)))
            {
                string name = member is ConstructorInfo ? "constructor" : member.Name;
                yield return $"{Describe(type)}.{name}: '{parameter.Name}' is optional";
            }
        }
    }

    private static bool IsReachable(Type type, MethodBase member)
        => member.IsPublic || ((member.IsFamily || member.IsFamilyOrAssembly) && !type.IsSealed);

    private static bool IsCallerInformation(ParameterInfo parameter)
        => parameter.IsDefined(typeof(CallerFilePathAttribute))
            || parameter.IsDefined(typeof(CallerLineNumberAttribute))
            || parameter.IsDefined(typeof(CallerMemberNameAttribute))
            || parameter.IsDefined(typeof(CallerArgumentExpressionAttribute));

    // The compiler writes a Deconstruct for exactly the records that have a primary constructor, and marks it
    // as its own - which tells a positional record from one with init properties, and from a hand-written one.
    internal static bool IsPositionalRecord(Type type)
        => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(static method => method.Name == "Deconstruct" && method.IsDefined(typeof(CompilerGeneratedAttribute)));

    private static string Describe(Type type)
    {
        string name = type.IsGenericType ? type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)] : type.Name;

        return type.DeclaringType is null ? name : $"{Describe(type.DeclaringType)}.{name}";
    }

    private static Type[] ExportedTypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return [.. exception.Types.Where(static type => type is { IsVisible: true })!];
        }
    }
}
