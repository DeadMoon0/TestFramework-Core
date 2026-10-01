using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins that a run writes its rendered lines to whatever method it is handed.
/// </summary>
/// <remarks>
/// The engine knows no test framework: a test passes the method that writes a line - <c>output.WriteLine</c>,
/// <c>TestContext.WriteLine</c>, <c>Console.WriteLine</c>. The damage this prevents is a run that passes and
/// prints nothing, which leaves a red test with no account of what happened.
/// </remarks>
public sealed class RunOutputTests
{
    [Fact]
    public async Task ARunWritesItsLinesToTheMethodItWasGiven()
    {
        List<string> lines = [];

        await Timeline.Create()
            .SetVariable("greeting", Var.Const("hello"))
            .Build()
            .SetupRun(lines.Add)
            .RunAsync();

        Assert.NotEmpty(lines);
    }

    [Fact]
    public async Task ARunWithServicesWritesItsLinesToTheMethodItWasGiven()
    {
        List<string> lines = [];

        await Timeline.Create()
            .SetVariable("greeting", Var.Const("hello"))
            .Build()
            .SetupRun(new EmptyServiceProvider(), lines.Add)
            .RunAsync();

        Assert.NotEmpty(lines);
    }
}
