using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Steps;

/// <summary>
/// Checks that a step's <c>Clone()</c> actually produced an independent copy, at the one place a step
/// definition is ever cloned.
/// </summary>
/// <remarks>
/// <para>
/// A timeline is authored once and may be run many times, so the framework clones each step definition
/// before applying that run's modifiers. Every step writes that clone by hand, which makes it discipline:
/// there are dozens of <c>Clone()</c> overrides and nothing has ever checked one.
/// </para>
/// <para>
/// Three of the ways it goes wrong are visible from outside the step, and all three are silent. This
/// checks those three, on the real instance, before the run starts - which beats a convention test over
/// types it would have to guess how to construct.
/// </para>
/// <para>
/// What it cannot check is whether the step deep-copied its <em>own</em> fields: only the step knows what
/// it holds. Making that unnecessary rather than checked - immutable step definitions, modifiers producing
/// new instances - is the deeper fix, and a much larger one.
/// </para>
/// </remarks>
internal static class StepCloneGuard
{
    /// <summary>
    /// Verifies a freshly made clone, and returns it so a caller can use this in place of the call.
    /// </summary>
    /// <param name="source">The authored step.</param>
    /// <param name="clone">What its <c>Clone()</c> returned.</param>
    /// <returns>The clone.</returns>
    internal static StepGeneric Verify(StepGeneric source, StepGeneric clone)
    {
        ArgumentNullException.ThrowIfNull(source);

        string stepType = source.GetType().Name;

        if (clone is null)
        {
            throw new FrameworkConfigurationException(
                $"'{stepType}.Clone()' returned null, so this step cannot be added to a timeline.",
                [$"Return a new {stepType} from Clone(), carrying this step's own fields."],
                []);
        }

        // Damage: the modifiers about to run - Name(...), WithTimeOut(...) - would edit the object the
        // test author is holding, so a second run of the same timeline would start already configured.
        if (ReferenceEquals(source, clone))
        {
            throw new FrameworkConfigurationException(
                $"'{stepType}.Clone()' returned the same instance instead of a copy.",
                [
                    $"Return 'new {stepType}(...).WithClonedOptions(this)' rather than 'this'.",
                ],
                []);
        }

        // Damage: the step silently loses whatever the subclass added - its fields, its overrides - and
        // runs as its own base class, which looks like the step simply not doing its job.
        if (clone.GetType() != source.GetType())
        {
            throw new FrameworkConfigurationException(
                $"'{stepType}.Clone()' returned a '{clone.GetType().Name}', so this step would run as its base class.",
                [
                    $"Give {stepType} its own Clone() override that returns a {stepType}.",
                ],
                [$"a base class in the chain returned its own type: {clone.GetType().Name}"]);
        }

        foreach (string shared in SharedOptions(source, clone))
        {
            // Damage: two steps holding one options object. Configuring either configures both, and
            // freezing either freezes both - so the second run fails on a write the first one allowed.
            throw new FrameworkConfigurationException(
                $"'{stepType}.Clone()' returned a copy that shares its {shared} with the original.",
                [
                    $"Let the new {stepType} build its own options and copy the values with WithClonedOptions(this).",
                    "Do not assign the original's options objects onto the clone.",
                ],
                []);
        }

        return clone;
    }

    private static IEnumerable<string> SharedOptions(StepGeneric source, StepGeneric clone)
    {
        Dictionary<string, IFreezable> original = [];

        foreach ((string name, IFreezable part) in source.FrameworkOptions())
        {
            original[name] = part;
        }

        foreach ((string name, IFreezable part) in clone.FrameworkOptions())
        {
            if (original.TryGetValue(name, out IFreezable? same) && ReferenceEquals(same, part))
            {
                yield return name;
            }
        }
    }
}
