using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Conventions;

/// <summary>
/// What a convention check looked at, so a caller can see what it did not.
/// </summary>
/// <param name="Checked">How many types were actually checked.</param>
/// <param name="Skipped">
/// The types the check could not reach, each with the reason. A check that quietly passes over what it
/// cannot construct reads as "all clear" when it means "I looked at four of forty".
/// </param>
public sealed record ConventionReport(int Checked, IReadOnlyList<string> Skipped)
{
    /// <summary>
    /// Reads as <c>checked 12, skipped 3</c>, for a suite that wants to log what ran.
    /// </summary>
    /// <returns>The summary.</returns>
    public override string ToString() => $"checked {this.Checked}, skipped {this.Skipped.Count}";
}

/// <summary>
/// The rules every package's steps have to follow, checked against a compiled assembly.
/// </summary>
/// <remarks>
/// <para>
/// Cloning and freezing are the framework's two pieces of pure discipline: dozens of hand-written
/// <c>Clone()</c> overrides and dozens of hand-written freeze guards, each one a line somebody could
/// forget, and nothing that notices when they do. These checks are the cheapest half of the answer -
/// the other half is <c>StepCloneGuard</c>, which watches the real objects as a run is planned.
/// </para>
/// <para>
/// A package's own suite calls these once against its own assembly, which is why they live in Core: a
/// rule that only Core's tests enforce is a rule only Core follows.
/// </para>
/// </remarks>
public static class StepConventions
{
    /// <summary>
    /// Every concrete step in the assembly declares its own <c>Clone()</c>.
    /// </summary>
    /// <remarks>
    /// The compiler already demands one from a step deriving straight from <c>Step&lt;T&gt;</c>. What it
    /// does not demand is a new override when a step derives from another <em>concrete</em> step: that
    /// subclass inherits a Clone() which builds the base type, so the step quietly runs as its base
    /// class and loses whatever the subclass added.
    /// </remarks>
    /// <param name="assembly">The assembly to check.</param>
    /// <returns>What was checked.</returns>
    /// <exception cref="FrameworkConfigurationException">When a step inherits somebody else's clone.</exception>
    public static ConventionReport AssertEveryStepClonesItself(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        List<Type> steps = [.. ConcreteTypesOf(assembly).Where(static type => typeof(StepGeneric).IsAssignableFrom(type))];
        List<string> offenders = [];

        foreach (Type step in steps)
        {
            if (DeclaresItsOwnClone(step))
            {
                continue;
            }

            offenders.Add($"{step.Name} inherits Clone() from {step.BaseType?.Name}");
        }

        if (offenders.Count != 0)
        {
            throw new FrameworkConfigurationException(
                $"{offenders.Count} step type(s) in '{assembly.GetName().Name}' do not clone themselves.",
                [
                    "Give each of them a Clone() override that returns its own type and copies its own fields.",
                    "Carry the framework's options across with WithClonedOptions(this).",
                ],
                [.. offenders.OrderBy(static offender => offender, StringComparer.Ordinal)]);
        }

        return new ConventionReport(steps.Count, []);
    }

    /// <summary>
    /// Freezing a thing in the assembly freezes what it is made of.
    /// </summary>
    /// <remarks>
    /// The failure this catches is a new part added to a type whose <c>Freeze</c> lists its parts by
    /// hand: everything looks frozen, and the one that was forgotten can still be written to after the
    /// run has settled. Only types the check can construct are covered, and the rest are reported as
    /// skipped rather than passed over.
    /// </remarks>
    /// <param name="assembly">The assembly to check.</param>
    /// <returns>What was checked, and what could not be.</returns>
    /// <exception cref="FrameworkConfigurationException">When freezing leaves a part unfrozen.</exception>
    public static ConventionReport AssertFreezingCascades(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        List<string> offenders = [];
        List<string> skipped = [];
        int checkedTypes = 0;

        foreach (Type type in ConcreteTypesOf(assembly).Where(static type => typeof(IFreezable).IsAssignableFrom(type)))
        {
            if (type.IsGenericTypeDefinition || type.GetConstructor(Type.EmptyTypes) is null)
            {
                // Nothing to do with discipline: the framework builds these itself, with arguments only
                // it has. They are covered where they are used, not here.
                skipped.Add($"{type.Name} (nothing can construct it without arguments)");
                continue;
            }

            IFreezable? instance;

            try
            {
                instance = (IFreezable?)Activator.CreateInstance(type);
            }
            catch (Exception exception) when (exception is MissingMethodException or MemberAccessException or TargetInvocationException)
            {
                skipped.Add($"{type.Name} ({exception.GetType().Name})");
                continue;
            }

            if (instance is null)
            {
                skipped.Add($"{type.Name} (no instance)");
                continue;
            }

            checkedTypes++;
            instance.Freeze();

            if (!instance.IsFrozen)
            {
                offenders.Add($"{type.Name} reports itself unfrozen after Freeze()");
            }

            foreach ((string name, IFreezable part) in FreezableParts(instance))
            {
                if (!part.IsFrozen)
                {
                    offenders.Add($"{type.Name}.{name} is still unfrozen after {type.Name}.Freeze()");
                }
            }
        }

        if (offenders.Count != 0)
        {
            throw new FrameworkConfigurationException(
                $"{offenders.Count} part(s) in '{assembly.GetName().Name}' survive a freeze.",
                [
                    "Freeze every freezable part in that type's Freeze(), or find the parts instead of listing them.",
                ],
                [.. offenders.OrderBy(static offender => offender, StringComparer.Ordinal)]);
        }

        return new ConventionReport(checkedTypes, [.. skipped.OrderBy(static entry => entry, StringComparer.Ordinal)]);
    }

    private static bool DeclaresItsOwnClone(Type step)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        return step.GetMethods(declared).Any(static method => method.Name is "Clone" or "CloneGeneric");
    }

    private static IEnumerable<(string Name, IFreezable Part)> FreezableParts(IFreezable instance)
    {
        foreach (PropertyInfo property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!typeof(IFreezable).IsAssignableFrom(property.PropertyType) || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            if (property.GetValue(instance) is IFreezable part)
            {
                yield return (property.Name, part);
            }
        }
    }

    private static IEnumerable<Type> ConcreteTypesOf(Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // A package whose optional dependency is absent still has steps worth checking.
            types = [.. exception.Types.Where(static type => type is not null)!];
        }

        return types.Where(static type => type is { IsClass: true, IsAbstract: false });
    }
}
