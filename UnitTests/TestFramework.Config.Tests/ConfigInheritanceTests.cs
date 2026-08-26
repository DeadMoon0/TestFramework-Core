using System;
using System.Collections.Generic;
using TestFramework.Config.Configuration;
using TestFramework.Core.Exceptions;
using Xunit;

namespace TestFramework.Config.Tests;

/// <summary>
/// What one configuration entry takes over from another, and what "did not state it" means.
/// </summary>
/// <remarks>
/// <para>
/// This exists because a package wrote the merge by hand and got it wrong. Its version asked "does this
/// differ from the default?" for seven of twenty-one values, which is not the same question as "did anybody
/// set this?" and cannot be made into it: a child that deliberately named the default was indistinguishable
/// from a child that said nothing, so the parent won and the child's choice vanished without a message.
/// </para>
/// <para>
/// The sharpest case in that package loosened a test rather than tightening one - a variant asking for strict
/// matching under a lenient parent stayed lenient - so these cases are about a child's stated value surviving,
/// whatever it happens to be.
/// </para>
/// </remarks>
public class ConfigInheritanceTests
{
    [Fact]
    public void AnEntryThatStandsAloneIsUnchanged()
    {
        Entry alone = new Entry { Browser = "firefox", Headless = true };

        Assert.Same(alone, Resolve(("shop", alone))["shop"]);
    }

    [Fact]
    public void AChildTakesOverEverythingItDidNotState()
    {
        IReadOnlyDictionary<string, Entry> resolved = Resolve(
            ("shop", new Entry { Browser = "firefox", Headless = false, Timeout = TimeSpan.FromSeconds(60) }),
            ("shop-mobile", new Entry { BasedOn = "shop", Device = "Narrow" }));

        Entry mobile = resolved["shop-mobile"];

        Assert.Equal("Narrow", mobile.Device);
        Assert.Equal("firefox", mobile.Browser);
        Assert.False(mobile.Headless);
        Assert.Equal(TimeSpan.FromSeconds(60), mobile.Timeout);
    }

    [Fact]
    public void AChildKeepsAStatedValueEvenWhenItEqualsTheDefault()
    {
        // The case the hand-written merge got wrong, in the three shapes it got wrong: a bool whose stated
        // value is the type's default, an enum whose stated value is the first member, and a bool the old
        // merge combined with 'or' so a child could never turn it back off.
        IReadOnlyDictionary<string, Entry> resolved = Resolve(
            ("lenient", new Entry { Browser = "firefox", Headless = true, Matching = Matching.Loose, IgnoreErrors = true }),
            ("strict", new Entry { BasedOn = "lenient", Headless = false, Matching = Matching.Strict, IgnoreErrors = false }));

        Entry strict = resolved["strict"];

        Assert.False(strict.Headless);
        Assert.Equal(Matching.Strict, strict.Matching);
        Assert.False(strict.IgnoreErrors);
    }

    [Fact]
    public void InheritanceReachesThroughAChain()
    {
        IReadOnlyDictionary<string, Entry> resolved = Resolve(
            ("base", new Entry { Browser = "webkit" }),
            ("middle", new Entry { BasedOn = "base", Headless = true }),
            ("leaf", new Entry { BasedOn = "middle", Device = "Narrow" }));

        Entry leaf = resolved["leaf"];

        Assert.Equal("webkit", leaf.Browser);
        Assert.True(leaf.Headless);
        Assert.Equal("Narrow", leaf.Device);
    }

    [Fact]
    public void AResolvedEntryPointsAtNobody()
    {
        // Otherwise it claims there is more to take over, which there is not - and a second pass over an
        // already-resolved set would go looking for a parent again.
        Entry child = Resolve(
            ("base", new Entry { Browser = "webkit" }),
            ("child", new Entry { BasedOn = "base" }))["child"];

        Assert.Null(child.BasedOn);
    }

    [Fact]
    public void ACycleIsRefusedAndNamed()
    {
        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => Resolve(
                ("a", new Entry { BasedOn = "b" }),
                ("b", new Entry { BasedOn = "a" })));

        Assert.Contains("inherits from itself", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AParentNobodyDeclaredIsRefusedWithWhatDoesExist()
    {
        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => Resolve(("shop", new Entry { BasedOn = "shp" })));

        Assert.Contains("not declared", failure.Message, StringComparison.Ordinal);
        Assert.Contains("shop", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordThatCannotExpressUnsetIsRefusedByName()
    {
        // The check that makes the whole thing hold. A non-nullable property cannot say "nobody set me", so
        // it could only ever take part silently and wrongly - refusing it is what stops the next package
        // rediscovering the bug this replaced.
        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => ConfigInheritance.Resolve(new Dictionary<string, Unstatable>
            {
                ["one"] = new Unstatable(),
            }));

        Assert.Contains("cannot express", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Headless", failure.Message, StringComparison.Ordinal);
        Assert.Contains("nullable", failure.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, Entry> Resolve(params (string Identifier, Entry Config)[] entries)
    {
        Dictionary<string, Entry> declared = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        foreach ((string identifier, Entry config) in entries)
        {
            declared[identifier] = config;
        }

        return ConfigInheritance.Resolve(declared);
    }

    private enum Matching
    {
        Strict = 0,
        Loose = 1,
    }

    /// <summary>A configuration record shaped the way the rule requires: every value can be unstated.</summary>
    private sealed record Entry : IInheritsConfig
    {
        public string? BasedOn { get; init; }

        public string? Browser { get; init; }

        public string? Device { get; init; }

        public bool? Headless { get; init; }

        public bool? IgnoreErrors { get; init; }

        public Matching? Matching { get; init; }

        public TimeSpan? Timeout { get; init; }
    }

    /// <summary>A record with a value that cannot be unstated, which is refused.</summary>
    private sealed record Unstatable : IInheritsConfig
    {
        public string? BasedOn { get; init; }

        public bool Headless { get; init; }
    }
}
