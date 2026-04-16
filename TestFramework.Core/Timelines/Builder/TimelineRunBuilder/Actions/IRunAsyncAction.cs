using System.Threading.Tasks;
using TestFramework.Core.Timelines;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

public interface IRunAsyncAction
{
    public Task<TimelineRun> RunAsync();
}