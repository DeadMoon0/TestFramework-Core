using TestFramework.Core.Artifacts;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins what a step's declarations turn into, now that the contract builds the entries itself.
/// </summary>
public sealed class StepIOContractTests
{
    [Fact]
    public void AReferenceToAVariableDeclaresItWithItsType()
    {
        StepIOContract contract = new();

        contract.Input(Var.Ref<int>(new VariableIdentifier("count")));

        StepIOEntry input = Assert.Single(contract.Inputs);
        Assert.Equal(("count", StepIOKind.Variable, true, typeof(int)), (input.Key, input.Kind, input.Required, input.DeclaredType));
    }

    [Fact]
    public void AReferenceHoldingAConstantDeclaresNothing()
    {
        // There is no variable to depend on; every step used to guard this by hand before reaching for Identifier!.
        StepIOContract contract = new();

        contract.Input(Var.Const(5)).OptionalInput<string>(null);

        Assert.Empty(contract.Inputs);
    }

    [Fact]
    public void AnArtifactIsDeclaredAsAnArtifact()
    {
        StepIOContract contract = new();

        contract.InputArtifact(new ArtifactIdentifier("order")).OutputArtifact(new ArtifactIdentifier("order"));

        Assert.Equal(StepIOKind.Artifact, Assert.Single(contract.Inputs).Kind);
        Assert.Equal(StepIOKind.Artifact, Assert.Single(contract.Outputs).Kind);
    }

    [Fact]
    public void AnOptionalOutputIsNotRequired()
    {
        StepIOContract contract = new();

        contract.OptionalOutput(new VariableIdentifier("maybe"), typeof(string));

        Assert.False(Assert.Single(contract.Outputs).Required);
    }

    [Fact]
    public void AFrozenContractTakesNoDeclaration()
    {
        StepIOContract contract = new();
        contract.Freeze();

        Assert.Throws<Exceptions.FrameworkStateException>(() => contract.Output(new VariableIdentifier("late")));
    }
}
