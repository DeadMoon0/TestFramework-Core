using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for finalizing a timeline definition.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IBuildAction
{
    /// <summary>
    /// Builds the configured timeline definition.
    /// </summary>
    public Timeline Build();
}