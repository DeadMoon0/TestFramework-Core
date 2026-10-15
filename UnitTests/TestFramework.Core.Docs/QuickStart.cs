#region snippet:quick-start
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;
using Xunit;
#endregion
using System.Threading.Tasks;

namespace TestFramework.Core.Docs.QuickStart;

#region snippet:quick-start
public class CoreSample
{
    private const string InputValue = "Alex";

    private static readonly Timeline _timeline = Timeline.Create()
        .SetVariable("name", InputValue)
        .Transform(Var.Ref<string>("name"), "greeting", name => $"Hello {name}")
        .AssertVariable(Var.Ref<string>("greeting"), greeting => greeting == $"Hello {InputValue}")
        .Build();

    [Fact]
    public async Task RunTimeline()
    {
        TimelineRun run = await _timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        using (var assertionScope = run.AssertionScope())
        {
            run.Variable<string>("greeting").Should().Exist().And().Be($"Hello {InputValue}");
        }
    }
}
#endregion
