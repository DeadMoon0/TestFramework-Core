using System;
using System.Runtime.CompilerServices;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;

namespace TestFramework.Config;

/// <summary>
/// Adds configuration-instance run setup helpers without introducing a Core-to-Config dependency.
/// </summary>
public static class TimelineConfigInstanceExtensions
{
    /// <summary>
    /// Creates a run builder for the timeline using a configuration instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The service provider built here belongs to the run, which disposes it when it ends. It used to be built and
    /// handed over as a plain provider that nothing disposed, so every singleton the configuration registered
    /// outlived the run.
    /// </para>
    /// <para>
    /// The caller's file and line are passed on, so the run is identified by the test that started it rather than
    /// by this method.
    /// </para>
    /// </remarks>
    /// <param name="timeline">The timeline to configure.</param>
    /// <param name="config">The configuration instance used to build the run service provider.</param>
    /// <param name="sourceFilePath">Filled in by the compiler; identifies the calling test's file.</param>
    /// <param name="sourceLineNumber">Filled in by the compiler; identifies the calling line.</param>
    public static ITimelineRunBuilder SetupRun(
        this Timeline timeline,
        ConfigInstance config,
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(config);
        return timeline.SetupRun(RunServices.Owned(config.BuildServiceProvider()), sourceFilePath, sourceLineNumber);
    }
}