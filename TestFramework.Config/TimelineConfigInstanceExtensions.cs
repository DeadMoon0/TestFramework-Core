using System;
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
    /// <param name="timeline">The timeline to configure.</param>
    /// <param name="config">The configuration instance used to build the run service provider.</param>
    public static ITimelineRunBuilder SetupRun(this Timeline timeline, ConfigInstance config)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(config);
        return timeline.SetupRun(config.BuildServiceProvider());
    }
}