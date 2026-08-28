using System;
using System.Linq;
using TestFramework.Config.Configuration;
using TestFramework.Core.Conventions;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Config.Tests;

/// <summary>
/// The family's rules, checked against this package rather than trusted to have been followed.
/// </summary>
/// <remarks>
/// <para>
/// These live in Core (<see cref="StepConventions"/>) and every package calls them on its own assembly: a
/// rule only Core's suite enforces is a rule only Core follows. This package had no such suite until the
/// release audit looked for one - it is the utility pack, so it was never on the list of packs being
/// migrated, and being off that list quietly meant being off this one.
/// </para>
/// <para>
/// A utility pack owes its consumers what Core owes everyone, so the rules bind here exactly as they bind an
/// edge pack. It ships no steps, so the two step checks are expected to find nothing and say so; they stay
/// because the day this package does add one is the day nobody remembers to add the check.
/// </para>
/// </remarks>
public class ConventionTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryStepInThisPackageClonesItself()
    {
        ConventionReport report = StepConventions.AssertEveryStepClonesItself(typeof(ConfigInheritance).Assembly);

        output.WriteLine(report.ToString());
    }

    [Fact]
    public void FreezingCascadesThroughThisPackagesParts()
    {
        ConventionReport report = StepConventions.AssertFreezingCascades(typeof(ConfigInheritance).Assembly);

        output.WriteLine(report.ToString());
        foreach (string skipped in report.Skipped)
        {
            output.WriteLine($"  skipped {skipped}");
        }
    }

    [Fact]
    public void ThisPackageSerialisesWithOneJsonLibrary()
    {
        // The family picked Newtonsoft.Json. Two libraries mean two sets of attributes, two notions of
        // what null means, and values that survive one round trip but not the other - and the seam shows
        // up as a bug in whichever package sits between them. Checked against the compiled assembly,
        // because a stray using is invisible in a diff.
        Assert.DoesNotContain(
            "System.Text.Json",
            typeof(ConfigInheritance).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name));
    }

    [Fact]
    public void ThisPackageKeepsItsInternalsToItself()
    {
        // Every package is a stranger to every other. A grant to another package is a private handshake:
        // two packages understand each other and a third cannot join, so what the favoured one may do stops
        // being what any of them may do - and the grant hides the fact that a surface is missing.
        ConventionReport report = StepConventions.AssertNoPackageSeesAnothersInternals(typeof(ConfigInheritance).Assembly);

        output.WriteLine(report.ToString());
    }
}
