using System;
using TestFrameworkCore.Stages;
using TestFrameworkCore.Timelines.Builder.TimelineBuilder;
using TestFrameworkCore.Timelines.Builder.TimelineRunBuilder;
using Xunit.Abstractions;

namespace TestFrameworkCore.Timelines;

public class Timeline : IFreezable
{
    public static ITimelineBuilder Create()
    {
        return new TimelineBuilder();
    }

    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        MainStage.Freeze();
    }

    private bool _readyToRun = false;
    internal bool ReadyToRun { get => _readyToRun; set { ((IFreezable)this).EnsureNotFrozen(); _readyToRun = value; } }

    public PreProcessableStage MainStage { get; internal set; } = new PreProcessableStage() { Name = "Main Stage", Description = "The Stage where all Main Steps are Executed." };

    internal Timeline(PreProcessableStage mainStage)
    {
        MainStage = mainStage;
    }

    public ITimelineRunBuilder SetupRun() => SetupRun(null, null);
    public ITimelineRunBuilder SetupRun(IServiceProvider? serviceProvider) => SetupRun(serviceProvider, null);
    public ITimelineRunBuilder SetupRun(ITestOutputHelper? outputHelper) => SetupRun(null, outputHelper);
    public ITimelineRunBuilder SetupRun(IServiceProvider? serviceProvider, ITestOutputHelper? outputHelper)
    {
        if (!ReadyToRun) throw new InvalidOperationException("The " + nameof(Timeline) + " cannot Setup a Run when the Builder did not finish Building.");
        serviceProvider ??= new EmptyServiceProvider();
        return new TimelineRunBuilder(serviceProvider, outputHelper, this, MainStage);
    }
}