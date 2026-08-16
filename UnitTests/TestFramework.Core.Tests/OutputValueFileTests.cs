using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers how the run's text output treats a value that was written to a file.
/// </summary>
/// <remarks>
/// The output exists to show the flow of a run. A value big enough to need a file is big enough to
/// bury that flow, so what it gets is a line naming it — not a hundred characters of its middle.
/// </remarks>
public sealed class OutputValueFileTests
{
    [Fact]
    public async Task AValueInAFileIsNamedRatherThanExcerpted()
    {
        string output = await Render(Body());

        Assert.Contains("values/report.txt", output, StringComparison.Ordinal);
        Assert.DoesNotContain("order 1 accepted", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValueInAFileSaysWhatItIsAndHowBig()
    {
        // Enough for a reader to decide whether to open it: what kind of thing it is, and what it
        // will cost them to look.
        string output = await Render(Body());

        Assert.Contains("length 82891", output, StringComparison.Ordinal);
        Assert.Contains("80,9 KB", output.Replace('.', ','), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryFileTheRunWroteIsListedInOnePlace()
    {
        // Scattered through the step panels these are found only by reading all of them. Collected,
        // they are a manifest of what the run produced — and of what a build is about to publish.
        string output = await Render(Body());

        Assert.Contains("Value Files", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunThatWroteNoFilesGetsNoSuchSection()
    {
        // A heading over nothing is a question a reader has to answer for themselves.
        string output = await Render(body: null);

        Assert.DoesNotContain("Value Files", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValueThatFitsIsStillShownInFull()
    {
        // The rule is about values that went to a file, not about values in general.
        string output = await Render(body: null);

        Assert.Contains("small enough to send", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHeaderNamesTheProjectRatherThanTheProcessThatHostedIt()
    {
        // The announced path is the host under a test runner, so taking it at face value put
        // "testhost.exe" on the first line every reader of a CI log sees — the same line for every
        // run in the suite.
        string output = await Render(body: null, Identity());

        Assert.Contains("Acme.Billing.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("testhost", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHeaderNamesTheProjectRatherThanPrintingItsWholePath()
    {
        // A full path wraps over three lines of the panel and buries the one word that identifies
        // the run among directories every other line of the log shares.
        string output = await Render(body: null, Identity());

        Assert.DoesNotContain(@"C:\agent", output, StringComparison.Ordinal);
        Assert.DoesNotContain(".csproj", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunWithNoIdentityStillNamesWhateverItWasGiven()
    {
        // Nothing resolved the identity, so the announced path is all there is. Showing nothing
        // would be worse than showing the host.
        string output = await Render(body: null, identity: null);

        Assert.Contains("project", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"C:\src\Acme.Billing.Tests\Acme.Billing.Tests.csproj", "Acme.Billing.Tests")]
    [InlineData(@"C:\agent\_work\1\s\bin\testhost.exe", "testhost")]
    [InlineData("/home/build/src/Acme.Web.Tests.dll", "Acme.Web.Tests")]
    [InlineData("Acme.Billing.Tests", "Acme.Billing.Tests")]
    public void AProjectIsNamedByItsLastSegmentWithoutItsExtension(string project, string expected)
    {
        // The last case is the one that bites: a bare assembly name is dotted too, and trimming its
        // last segment would turn Acme.Billing.Tests into Acme.Billing — a project that does not
        // exist, sitting in the list beside the one that does.
        Assert.Equal(expected, TestIdentity.ShortNameOf(project));
    }

    [Fact]
    public void AProjectWithNoNameToShortenIsLeftAlone()
    {
        Assert.Equal(string.Empty, TestIdentity.ShortNameOf(string.Empty));
        Assert.Equal(@"C:\src\", TestIdentity.ShortNameOf(@"C:\src\"));
    }

    private static TestIdentity Identity() => new()
    {
        DisplayName = "LargeValues",
        Framework = TestFrameworkKind.XUnit,
        AssemblyPath = @"C:\agent\_work\1\s\bin\testhost.exe",
        AssemblyName = "Acme.Billing.Tests",
        ProjectFilePath = "Acme.Billing.Tests.csproj"
    };

    private static async Task<string> Render(DebugValueBody? body) => await Render(body, identity: null);

    private static async Task<string> Render(DebugValueBody? body, TestIdentity? identity)
    {
        RecordingOutput output = new();
        OutputRunDebugger debugger = new(output);

        DebugStepState step = new()
        {
            Name = "Transform",
            Description = string.Empty,
            RetryOptions = new RetryOptions(),
            ErrorHandlingOptions = new ErrorHandlingOptions(),
            TimeOutOptions = new TimeOutOptions(),
            LabelOptions = new LabelOptions(),
            ExecutionOptions = new ExecutionOptions(),
            IOContract = new StepIOContract(),
            Phase = StepExecutionPhase.Act,
            DoesReturn = false
        };

        step.IOContract.Outputs.Add(new StepIOEntry("report", StepIOKind.Variable, true, typeof(string)));

        await debugger.SignalInitTimelineRunAsync("session", "timeline", "project", new TimelineRunStructure
        {
            Stages = [new DebugStageState { Name = "Main", Description = string.Empty, Steps = [step] }],
            Variables = new Dictionary<VariableIdentifier, DebugValue>(),
            Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
        }, identity);

        await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);
        await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);

        await debugger.SignalValueUpdateAsync("session", "report", DebugValueKind.Variable, "Main", 0, new DebugValueEnvelope
        {
            Kind = DebugValueKind.Variable,
            TypeName = "System.String",
            DisplayText = body is null ? "\"small enough to send\"" : "\"order 1 accepted order 2 accepted\"",
            SchemaKey = DebugValueSchemaKeys.Text,
            Description = new DebugValueDescription
            {
                Summary = body is null ? "\"small enough to send\"" : "\"order 1 accepted order 2 accepted\"",
                Shape = DebugValueShape.Text,
                Fields =
                [
                    new DebugValueField { Name = "type", Value = "String" },
                    new DebugValueField { Name = "length", Value = body is null ? "20" : "82891" }
                ],
                Body = body
            }
        });

        await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);
        await debugger.SignalTimelineRunFinishedAsync("session");

        return Flatten(output.Lines);
    }

    /// <summary>
    /// Strips the box drawing and joins the lines, so an assertion is about what the output says.
    /// </summary>
    /// <remarks>
    /// The panels wrap to a fixed width, so any given phrase may be split across two lines with a
    /// border between the halves. A test that asserted on the raw text would be a test of the column
    /// width.
    /// </remarks>
    private static string Flatten(IReadOnlyList<string> lines)
    {
        string joined = string.Join(" ", lines.Select(line => new string([.. line.Where(character => !"│─╭╮╰╯├┤═".Contains(character, StringComparison.Ordinal))])));

        return string.Join(" ", joined.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static DebugValueBody Body() => new()
    {
        Path = @"C:\runs\Sample-1234abcd\values\report.txt",
        RelativePath = "values/report.txt",
        SizeInBytes = 82_891,
        ContentHash = "ABCD"
    };

    private sealed class RecordingOutput : ITestOutputHelper
    {
        private readonly List<string> lines = [];

        internal IReadOnlyList<string> Lines => lines;

        public void WriteLine(string message) => lines.Add(message);

        public void WriteLine(string format, params object[] args) => lines.Add(string.Format(format, args));
    }
}
