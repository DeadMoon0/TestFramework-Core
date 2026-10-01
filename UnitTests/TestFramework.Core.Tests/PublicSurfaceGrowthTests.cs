using System.Linq;
using System.Runtime.CompilerServices;
using TestFramework.Core.Conventions;
using TestFramework.Core.Environment.Graph;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins the rule that keeps Core's public surface growable, and holds Core to it.
/// </summary>
/// <remarks>
/// The damage it prevents is a knob added the cheap way. An optional parameter or a positional-record member
/// added to Core compiles in every package and then throws <c>MissingMethodException</c> in each of them at run
/// time, because they were compiled against the old signature. It has happened once already.
/// </remarks>
public sealed class PublicSurfaceGrowthTests
{
    [Fact]
    public void CoresPublicSurfaceCanGrow()
        => Assert.True(StepConventions.AssertPublicSurfaceCanGrow(typeof(ResourceGraph).Assembly).Checked > 0);

    [Fact]
    public void AnOptionalParameterIsFound()
        => Assert.Contains(StepConventions.OptionalParametersOf(typeof(WithOptional)), static offender => offender.Contains("'flag'", System.StringComparison.Ordinal));

    [Fact]
    public void CallerInformationIsNotAnOptionalParameterAnybodyPasses()
        => Assert.Empty(StepConventions.OptionalParametersOf(typeof(WithCallerInformation)));

    [Fact]
    public void AProtectedOptionalParameterIsPartOfTheSurface()
        => Assert.NotEmpty(StepConventions.OptionalParametersOf(typeof(OpenWithProtected)));

    [Fact]
    public void APositionalRecordIsToldFromAnInitRecord()
    {
        Assert.True(StepConventions.IsPositionalRecord(typeof(Positional)));
        Assert.False(StepConventions.IsPositionalRecord(typeof(WithInitProperties)));
        Assert.False(StepConventions.IsPositionalRecord(typeof(HandWrittenDeconstruct)));
    }

    [Fact]
    public void ADefaultOnAClosedShapeIsStillAnOptionalParameter()
        => Assert.NotEmpty(StepConventions.OptionalParametersOf(typeof(ClosedWithDefault)));

    public sealed class WithOptional
    {
        public void Run(string name, bool flag = false) { _ = name; _ = flag; }
    }

    public sealed class WithCallerInformation
    {
        public void Run([CallerFilePath] string file = "", [CallerLineNumber] int line = 0) { _ = file; _ = line; }
    }

    public abstract class OpenWithProtected
    {
        protected void Run(bool flag = false) { _ = flag; }
    }

    public sealed record Positional(string Name);

    public sealed record WithInitProperties
    {
        public required string Name { get; init; }
    }

    public sealed class HandWrittenDeconstruct
    {
        public void Deconstruct(out string name) => name = string.Empty;
    }

    [ClosedShape("A fixture.")]
    public sealed record ClosedWithDefault(string Name, int Count = 0);
}
