using System.ComponentModel;
using TestFramework.Core.Timelines.Builder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for finalizing a timeline definition.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IBuildAction : IImplementedByCore
{
    /// <summary>
    /// Builds the configured timeline definition.
    /// </summary>
    public Timeline Build();
}