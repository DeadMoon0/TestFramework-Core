using System;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Runner;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// What a run decided on the caller's behalf, and whether the finished run can still say so.
/// </summary>
/// <remarks>
/// §5 lets a value default on three conditions, and the third - that the effective value is readable from
/// the frozen run - was satisfied nowhere in the family. A run could say what it did and not what it did it
/// with, so a suite that passed could not answer "which browser proved this".
/// </remarks>
public class EffectiveSettingsTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AFinishedRunStillSaysWhatItDecided()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new RecordingStep("ui.browser", "Browser", "chromium")).Name("records")
            .Build();

        TimelineRun run = await timeline.SetupRun(outputHelper: output).RunAsync();

        run.EnsureRanToCompletion();

        EffectiveSetting recorded = Assert.Single(run.EffectiveSettings.Snapshot());
        Assert.Equal("ui.browser", recorded.Source);
        Assert.Equal("Browser", recorded.Name);
        Assert.Equal("chromium", recorded.Value);
    }

    [Fact]
    public async Task TheRecordClosesWithTheRun()
    {
        // §2: everything under a run freezes when its part is done. A record that could still be written
        // after the run finished would be a snapshot that changes after it was handed over.
        Timeline timeline = Timeline.Create()
            .Trigger(new RecordingStep("ui.browser", "Browser", "chromium")).Name("records")
            .Build();

        TimelineRun run = await timeline.SetupRun(outputHelper: output).RunAsync();

        FrameworkStateException refused = Assert.Throws<FrameworkStateException>(
            () => run.EffectiveSettings.Record("ui.browser", "Browser", "firefox"));

        Assert.Contains("has finished", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordingTheSameThingTwiceIsFineAndDisagreeingIsNot()
    {
        EffectiveSettings settings = new EffectiveSettings();

        settings.Record("docker", "azurite:Image", "azurite:3.35.0");
        settings.Record("docker", "azurite:Image", "azurite:3.35.0");

        // One run started one container. Two answers means something decided it twice, which is worth
        // hearing about rather than resolving by whoever wrote last.
        FrameworkConfigurationException conflict = Assert.Throws<FrameworkConfigurationException>(
            () => settings.Record("docker", "azurite:Image", "azurite:3.34.0"));

        Assert.Contains("already recorded", conflict.Message, StringComparison.Ordinal);
        Assert.Equal("azurite:3.35.0", Assert.Single(settings.Snapshot()).Value);
    }

    [Fact]
    public void TwoSourcesMayDecideTheSameName()
    {
        // The source is part of the key precisely so a second package recording its own "Image" is not a
        // collision to be worked around with a prefix everyone invents differently.
        EffectiveSettings settings = new EffectiveSettings();

        settings.Record("docker.azure", "Image", "azurite:3.35.0");
        settings.Record("docker.web", "Image", "nginx:1.27");

        Assert.Equal(2, settings.Snapshot().Count);
        Assert.True(settings.TryGet("docker.web", "Image", out string? web));
        Assert.Equal("nginx:1.27", web);
    }

    private sealed class RecordingStep(string source, string name, string value) : Step<EmptyStepResultContext>
    {
        public override string Name => "Recording";

        public override string Description => "Records what this run resolved.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new RecordingStep(source, name, value).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.EffectiveSettings.Record(source, name, value);

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }
}
