using System.Threading.Tasks;
using TestFramework.Core.Timelines;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

/// <summary>
/// Adds the fluent verb for executing a configured timeline run.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRunAsyncAction
{
    /// <summary>
    /// Executes the configured timeline run asynchronously.
    /// </summary>
    public Task<TimelineRun> RunAsync();
}