using TestFramework.Core;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Simple;
using System.Runtime.Versioning;

namespace TestFramework.Simple.Tests;

public class MessageBoxTriggerTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Execute_UsesResolvedMessageAndCaptionValues()
    {
        MessageBoxTrigger trigger = new(Var.Const("Hello"), Var.Const("Greeting"));
        List<(string Text, string Caption)> calls = [];
        Func<string, string, int> originalInvoker = MessageBoxTrigger.MessageBoxInvoker;

        try
        {
            MessageBoxTrigger.MessageBoxInvoker = (text, caption) =>
            {
                calls.Add((text, caption));
                return 0;
            };

            await trigger.Execute(new EmptyServiceProvider(), new VariableStore(new TestFramework.Core.Logging.ScopedLogger(null), new TestFramework.Core.Debugger.DebuggingRunSession(TestFramework.Core.Debugger.EmptyRunDebugger.CreateNew())), new TestFramework.Core.Artifacts.ArtifactStore(new TestFramework.Core.Logging.ScopedLogger(null), new TestFramework.Core.Debugger.DebuggingRunSession(TestFramework.Core.Debugger.EmptyRunDebugger.CreateNew())), new TestFramework.Core.Logging.ScopedLogger(null), CancellationToken.None);
        }
        finally
        {
            MessageBoxTrigger.MessageBoxInvoker = originalInvoker;
        }

        Assert.Single(calls);
        Assert.Equal(("Hello", "Greeting"), calls[0]);
    }

    [Fact]
    public void MessageBoxTrigger_IsMarkedAsWindowsOnly()
    {
        SupportedOSPlatformAttribute? attribute = typeof(MessageBoxTrigger).GetCustomAttributes(typeof(SupportedOSPlatformAttribute), false)
            .OfType<SupportedOSPlatformAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal("windows", attribute!.PlatformName);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
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
    [SupportedOSPlatform("windows")]
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