using System.Threading.Tasks;

namespace TestFrameworkCore.Timelines.Builder.TimelineRunBuilder.Actions;

public interface IRunAsyncAction
{
    public Task<TimelineRun> RunAsync();
}