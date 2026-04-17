using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Simple;

namespace TestFramework.Simple.Tests;

public class MessageBoxTriggerTests
{
    [Fact]
    public void DeclareIO_AddsMessageAndCaptionInputs()
    {
        MessageBoxTrigger trigger = new(Var.Ref<string>("msg"), Var.Ref<string>("caption"));
        StepIOContract contract = new();

        trigger.DeclareIO(contract);

        Assert.Collection(
            contract.Inputs,
            entry =>
            {
                Assert.Equal("msg", entry.Key);
                Assert.Equal(typeof(string), entry.DeclaredType);
                Assert.True(entry.Required);
            },
            entry =>
            {
                Assert.Equal("caption", entry.Key);
                Assert.Equal(typeof(string), entry.DeclaredType);
                Assert.True(entry.Required);
            });
    }

    [Fact]
    public void Clone_PreservesStepOptions()
    {
        MessageBoxTrigger original = new(Var.Ref<string>("msg"), Var.Ref<string>("caption"));
        original.LabelOptions.Label = "show-message";
        original.ExecutionOptions.RunExclusively = true;

        MessageBoxTrigger clone = (MessageBoxTrigger)original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal("show-message", clone.LabelOptions.Label);
        Assert.True(clone.ExecutionOptions.RunExclusively);
    }
}