using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.Simple;
using Xunit.Abstractions;

namespace TestFramework.Simple.Tests;

public class MessageTriggerTests
{
    [Fact]
    public async Task Execute_WritesTheCaptionedMessageToTheRunLog()
    {
        RecordingOutput output = new();
        Timeline timeline = Timeline.Create()
            .Trigger(SimpleExt.Trigger.Message("Hello", "Greeting"))
            .Build();

        TimelineRun run = await timeline.SetupRun(output).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains("[Greeting] Hello", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_ResolvesMessageAndCaptionFromVariables()
    {
        RecordingOutput output = new();
        Timeline timeline = Timeline.Create()
            .Trigger(SimpleExt.Trigger.Message(Var.Ref<string>("msg"), Var.Ref<string>("caption")))
            .Build();

        TimelineRun run = await timeline.SetupRun(output)
            .AddVariable("msg", "resolved body")
            .AddVariable("caption", "resolved caption")
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains("[resolved caption] resolved body", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Message_WithoutACaption_UsesTheDefaultCaption()
    {
        RecordingOutput output = new();
        Timeline timeline = Timeline.Create()
            .Trigger(SimpleExt.Trigger.Message("body only"))
            .Build();

        TimelineRun run = await timeline.SetupRun(output).RunAsync();

        run.EnsureRanToCompletion();
        Assert.Contains("[Message] body only", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void MessageTrigger_IsNotRestrictedToWindows()
    {
        // The whole point of this trigger is that it runs where MessageBoxTrigger cannot.
        Assert.Empty(typeof(MessageTrigger).GetCustomAttributes(typeof(SupportedOSPlatformAttribute), false));
    }

    [Fact]
    public void DeclareIO_AddsMessageAndCaptionInputs()
    {
        MessageTrigger trigger = new(Var.Ref<string>("msg"), Var.Ref<string>("caption"));
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
        MessageTrigger original = new(Var.Ref<string>("msg"), Var.Ref<string>("caption"));
        original.LabelOptions.Label = "log-message";
        original.ExecutionOptions.ParallelizationMode = StepParallelizationMode.DoNotParallelize;

        MessageTrigger clone = (MessageTrigger)original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal("log-message", clone.LabelOptions.Label);
        Assert.Equal(StepParallelizationMode.DoNotParallelize, clone.ExecutionOptions.ParallelizationMode);
    }

    private sealed class RecordingOutput : ITestOutputHelper
    {
        private readonly System.Text.StringBuilder _builder = new();

        public string Text
        {
            get { lock (_builder) { return _builder.ToString(); } }
        }

        public void WriteLine(string message)
        {
            lock (_builder) { _builder.AppendLine(message); }
        }

        public void WriteLine(string format, params object[] args)
        {
            lock (_builder) { _builder.AppendLine(string.Format(format, args)); }
        }
    }
}
