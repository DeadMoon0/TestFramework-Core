using TestFramework.Core.Steps;
using TestFramework.Core.Environment.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Simple;
using System.Runtime.Versioning;

namespace TestFramework.Simple.Tests;

public class MessageBoxTriggerTests
{
    [Theory]
    [InlineData(null, true, false, true, true)]    // a developer at the screen
    [InlineData(null, false, false, true, false)]  // a service or a headless test host
    [InlineData(null, true, true, true, false)]    // a build agent, even an interactive one
    [InlineData("off", true, false, true, false)]  // the switch overrides an interactive session
    [InlineData("on", true, true, true, true)]     // and a build agent
    [InlineData(" OFF ", true, false, true, false)]
    [InlineData("on", true, false, false, false)]  // nothing overrides there being no dialog to show
    public void TheDialogIsShown_OnlyWhereSomebodyCanAnswerIt(string? switchValue, bool interactive, bool onCi, bool onWindows, bool shown)
        => Assert.Equal(shown, MessageBoxDisplay.Decide(switchValue, interactive, onCi, onWindows).Show);

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Execute_HandsTheResolvedMessageAndCaptionToItsPresenter()
    {
        List<(string Text, string Caption)> calls = [];
        MessageBoxTrigger trigger = new(Var.Const("Hello"), Var.Const("Greeting"), (text, caption) =>
        {
            calls.Add((text, caption));
            return 0;
        });
        string? previous = System.Environment.GetEnvironmentVariable(MessageBoxDisplay.SwitchVariable);

        try
        {
            System.Environment.SetEnvironmentVariable(MessageBoxDisplay.SwitchVariable, "on");
            await trigger.Execute(RunContext.Detached());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(MessageBoxDisplay.SwitchVariable, previous);
        }

        Assert.Equal(OperatingSystem.IsWindows() ? [("Hello", "Greeting")] : [], calls);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Clone_KeepsThePresenter()
    {
        List<string> calls = [];
        MessageBoxTrigger original = new(Var.Const("Hello"), Var.Const("Greeting"), (text, _) =>
        {
            calls.Add(text);
            return 0;
        });

        MessageBoxTrigger clone = (MessageBoxTrigger)original.Clone();
        string? previous = System.Environment.GetEnvironmentVariable(MessageBoxDisplay.SwitchVariable);

        try
        {
            System.Environment.SetEnvironmentVariable(MessageBoxDisplay.SwitchVariable, "on");
            await clone.Execute(RunContext.Detached());
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(MessageBoxDisplay.SwitchVariable, previous);
        }

        Assert.NotSame(original, clone);
        Assert.Equal(OperatingSystem.IsWindows() ? ["Hello"] : [], calls);
    }

    [Fact]
    public void NoProcessWideSwitch_ChangesHowEveryMessageBoxIsShown()
    {
        Assert.Empty(typeof(MessageBoxTrigger).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
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
        original.ExecutionOptions.ParallelizationMode = StepParallelizationMode.DoNotParallelize;

        MessageBoxTrigger clone = (MessageBoxTrigger)original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal("show-message", clone.LabelOptions.Label);
        Assert.Equal(StepParallelizationMode.DoNotParallelize, clone.ExecutionOptions.ParallelizationMode);
    }
}