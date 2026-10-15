using System.Threading.Tasks;
using TestFramework.Core.Timelines;

using System.ComponentModel;
using TestFramework.Core.Timelines.Builder;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

/// <summary>
/// Adds the fluent verb for executing a configured timeline run.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRunAsyncAction : IImplementedByCore
{
    /// <summary>
    /// Executes the configured timeline run asynchronously.
    /// </summary>
    public Task<TimelineRun> RunAsync();
}