#region snippet:readme-first-run
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;
using Xunit;
#endregion
using System.Threading.Tasks;

namespace TestFramework.Core.Docs.Readme;

#region snippet:readme-first-run
public class SampleIntegrationTest
{
    private const string InputValue = "Alex";

    private static readonly Timeline _timeline = Timeline.Create()
        .SetVariable("name", Var.Const(InputValue))
        .Transform("greeting", Var.Ref<string>("name"), name => $"Hello {name}")
        .AssertVariable(Var.Ref<string>("greeting"), greeting => greeting == $"Hello {InputValue}")
        .Build();

    [Fact]
    public async Task CanRunTimeline()
    {
        ConfigInstance config = ConfigInstance
            .Create()
            .OverrideConfig("TestSettings:Environment", "Local")
            .AddService(services =>
            {
                services.AddSingleton<ISystemClock, SystemClock>();
            })
            .Build();

        // The run owns the services it builds from the configuration, and disposes them when it ends.
        TimelineRun run = await _timeline.SetupRun(config).RunAsync();

        run.EnsureRanToCompletion();

        using (var assertionScope = run.AssertionScope())
        {
            run.Variable<string>("greeting").Should().Exist().And().Be($"Hello {InputValue}");
        }
    }

    private interface ISystemClock { }
    private sealed class SystemClock : ISystemClock { }
}
#endregion
