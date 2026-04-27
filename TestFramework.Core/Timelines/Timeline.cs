using System;
using TestFramework.Core;
using TestFramework.Core.Stages;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using Xunit.Abstractions;

namespace TestFramework.Core.Timelines;

/// <summary>
/// Represents an immutable timeline definition that can be built once and executed many times.
/// </summary>
public class Timeline : IFreezable
{
    /// <summary>
    /// Starts a new timeline builder using the consumer-first fluent API.
    /// </summary>
    public static ITimelineBuilder Create()
    {
        return new TimelineBuilder();
    }

    /// <summary>
    /// Gets a value indicating whether the timeline has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the timeline definition and its main stage.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        MainStage.Freeze();
    }

    private bool _readyToRun = false;
    internal bool ReadyToRun { get => _readyToRun; set { ((IFreezable)this).EnsureNotFrozen(); _readyToRun = value; } }

    /// <summary>
    /// Gets the main stage that contains the timeline's emitted steps before execution preprocessing.
    /// </summary>
    public PreProcessableStage MainStage { get; internal set; } = new PreProcessableStage() { Name = "Main Stage", Description = "The Stage where all Main Steps are Executed." };

    internal Timeline(PreProcessableStage mainStage)
    {
        MainStage = mainStage;
    }

    /// <summary>
    /// Creates a run builder for this timeline using default services and no test output helper.
    /// </summary>
    public ITimelineRunBuilder SetupRun() => SetupRun(null, null);

    /// <summary>
    /// Creates a run builder for this timeline using the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider available to steps and environment setup.</param>
    public ITimelineRunBuilder SetupRun(IServiceProvider? serviceProvider) => SetupRun(serviceProvider, null);

    /// <summary>
    /// Creates a run builder for this timeline using the provided xUnit output helper.
    /// </summary>
    /// <param name="outputHelper">The output helper that receives timeline log output.</param>
    public ITimelineRunBuilder SetupRun(ITestOutputHelper? outputHelper) => SetupRun(null, outputHelper);

    /// <summary>
    /// Creates a run builder for this timeline.
    /// </summary>
    /// <param name="serviceProvider">The service provider available to steps and environment setup.</param>
    /// <param name="outputHelper">The output helper that receives timeline log output.</param>
    /// <returns>A run builder that can be configured and executed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the timeline has not been built completely yet.</exception>
    public ITimelineRunBuilder SetupRun(IServiceProvider? serviceProvider, ITestOutputHelper? outputHelper)
    {
        if (!ReadyToRun) throw new InvalidOperationException("The " + nameof(Timeline) + " cannot Setup a Run when the Builder did not finish Building.");
        serviceProvider ??= new EmptyServiceProvider();
        return new TimelineRunBuilder(serviceProvider, outputHelper, this, MainStage);
    }
}