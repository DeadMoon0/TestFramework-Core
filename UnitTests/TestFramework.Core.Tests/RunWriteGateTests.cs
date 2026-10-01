using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Runner;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins that every channel of a run's live state follows the one rule its gate decides.
/// </summary>
/// <remarks>
/// The damage it prevents is a channel with a rule of its own. Each store used to keep its own "finished" flag
/// and its own idea of what a refusal is, so a new channel was writable on a finished run until somebody added
/// it to a list, and the same mistake threw in one place and was ignored in another.
/// </remarks>
public sealed class RunWriteGateTests
{
    public static TheoryData<string> StateChannels => new() { "variable", "artifact", "run state", "effective setting", "resource value", "environment state" };

    public static TheoryData<string> AttemptChannels => new() { "variable", "artifact", "effective setting", "widget" };

    [Theory]
    [MemberData(nameof(StateChannels))]
    public void AFinishedRunRefusesEveryStateChannelTheSameWay(string channel)
    {
        Run run = new();
        run.Gate.Finish(run.Logger);

        FrameworkStateException refused = Assert.Throws<FrameworkStateException>(() => run.Write(channel, run.Unrestricted()));

        Assert.StartsWith($"The run has finished, so it takes no new {channel}:", refused.FriendlyMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AFinishedRunDropsAWidgetInsteadOfThrowing()
    {
        // Widgets are evidence. A debugging aid that threw could change what the code around a finished run
        // sees, so a refused one is dropped and reported, whatever the reason.
        Run run = new();
        run.Gate.Finish(run.Logger);

        Assert.Null(run.Widgets(run.Unrestricted()).Publish(Run.Shot("after-the-end")));
    }

    [Theory]
    [MemberData(nameof(AttemptChannels))]
    public void AnAbandonedAttemptsWriteIsDroppedOnEveryChannel(string channel)
    {
        Run run = new();
        Views abandoned = run.AbandonedAttempt();

        run.Write(channel, abandoned);

        Assert.False(run.Holds(channel), $"the abandoned attempt's {channel} was kept");
    }

    [Fact]
    public void AnAbandonedAttemptCannotOpenRunState()
    {
        // The one channel that hands something back cannot drop a write quietly: a slot that is not kept
        // cannot be returned, and one opened anyway would be a live object nobody closes.
        Run run = new();
        Views abandoned = run.AbandonedAttempt();

        Assert.Throws<FrameworkStateException>(() => abandoned.Variables.RunState.GetOrAdd(() => new List<int>()));
        Assert.False(run.Variables.RunState.TryGet(out List<int>? _));
    }

    [Theory]
    [MemberData(nameof(StateChannels))]
    public void AFinishedRunStillReadsWhatItKept(string channel)
    {
        Run run = new();
        run.Write(channel, run.Unrestricted());
        run.Gate.Finish(run.Logger);

        Assert.True(run.Holds(channel), $"the finished run lost its {channel}");
    }

    [Fact]
    public async Task FinishWaitsForAWriteItAlreadyAdmitted()
    {
        // Admitted before the end, landing after it, would change a run that already said it was a snapshot.
        RunWriteGate gate = new();
        Assert.True(gate.Unrestricted.TryAdmit(RunChannel.Variable, "slow", new ScopedLogger(null), out RunWriteTicket? ticket));

        Task finishing = Task.Run(() => gate.Finish(new ScopedLogger(null)));

        Assert.NotSame(finishing, await Task.WhenAny(finishing, Task.Delay(TimeSpan.FromMilliseconds(200))));

        ticket.Dispose();

        Assert.Same(finishing, await Task.WhenAny(finishing, Task.Delay(TimeSpan.FromSeconds(5))));
    }

    private sealed class Run
    {
        private static readonly byte[] Png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        private static readonly ValueKey Key = new(ValueNames.ConnectionString);

        internal Run()
        {
            this.Session = new DebuggingRunSession(new EmptyRunDebugger());
            this.Logger = ScopedLogger.CreateWithDebuggerSession(this.Session);
            this.Variables = new VariableStore(this.Logger, this.Session, this.Gate);
            this.Artifacts = new ArtifactStore(this.Logger, this.Session, this.Gate);
            this.Values = new ResourceValueStore(this.Gate.Unrestricted, this.Logger);
            this.Environment = new EnvComponentContext(this.Gate.Unrestricted, this.Logger);
        }

        internal RunWriteGate Gate { get; } = new();

        internal DebuggingRunSession Session { get; }

        internal ScopedLogger Logger { get; }

        internal VariableStore Variables { get; }

        internal ArtifactStore Artifacts { get; }

        internal ResourceValueStore Values { get; }

        internal EnvComponentContext Environment { get; }

        internal Views Unrestricted() => new(this.Variables, this.Artifacts);

        internal Views AbandonedAttempt()
        {
            StepAttemptGate attempts = new();
            StepAttempt first = attempts.Begin("slow-step", 1);
            Views views = new(this.Variables.ForAttempt(attempts, first), this.Artifacts.ForAttempt(attempts, first));

            // The run moves on to a retry, and the first attempt loses its right to write.
            attempts.Begin("slow-step", 2);

            return views;
        }

        internal WidgetStore Widgets(Views views) => new(this.Session, views.Variables.Writer, this.Logger);

        internal void Write(string channel, Views views)
        {
            switch (channel)
            {
                case "variable":
                    views.Variables.SetVariable("written", 1);
                    break;
                case "artifact":
                    views.Artifacts.Add<GateArtifactDescriber, GateArtifactData, GateArtifactReference>(new ArtifactIdentifier("written"), new GateArtifactReference());
                    break;
                case "run state":
                    views.Variables.RunState.GetOrAdd(() => new List<int>());
                    break;
                case "effective setting":
                    views.Variables.EffectiveSettings.Record("test", "written", "yes");
                    break;
                case "resource value":
                    this.Values.Declare("test.kind", "written", Key, "value", "test", secret: false);
                    break;
                case "environment state":
                    this.Environment.SetState(new EnvComponentIdentifier("written"), null, EnvComponentScope.Run);
                    break;
                case "widget":
                    this.Widgets(views).Publish(Shot("written"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        internal bool Holds(string channel) => channel switch
        {
            "variable" => this.Variables.TryGetVariable(new VariableIdentifier("written"), out int _),
            "artifact" => System.Linq.Enumerable.Any(this.Artifacts.GetAll(), static held => held.Identifier.Identifier == "written"),
            "run state" => this.Variables.RunState.TryGet(out List<int>? _),
            "effective setting" => this.Variables.EffectiveSettings.TryGet("test", "written", out string? _),
            "resource value" => this.Values.TryGet("test.kind", "written", Key, out ResolvedValue? _),
            "environment state" => this.Environment.Contains(new EnvComponentIdentifier("written")),
            "widget" => false,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
        };

        internal static Widget Shot(string name) => new()
        {
            Kind = WidgetKinds.Screenshot,
            Name = name,
            Form = DebugPreviewForm.Image,
            Bytes = Png,
        };
    }

    private sealed record Views(VariableStore Variables, ArtifactStore Artifacts);

    private sealed class GateArtifactDescriber : ArtifactDescriber<GateArtifactDescriber, GateArtifactData, GateArtifactReference>
    {
        public override Task Setup(RunContext context, GateArtifactData data, GateArtifactReference reference) => Task.CompletedTask;

        public override Task Deconstruct(RunContext context, GateArtifactReference reference) => Task.CompletedTask;

        public override string ToString() => "gate-test-artifact";
    }

    private sealed class GateArtifactData : ArtifactData<GateArtifactData, GateArtifactDescriber, GateArtifactReference>
    {
        public override string ToString() => "gate-test-data";
    }

    private sealed class GateArtifactReference : ArtifactReference<GateArtifactReference, GateArtifactDescriber, GateArtifactData>
    {
        public override Task<ArtifactResolveResult<GateArtifactDescriber, GateArtifactData, GateArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<GateArtifactDescriber, GateArtifactData, GateArtifactReference> { Found = false });

        public override void DeclareIO(Steps.Options.StepIOContract contract)
        {
        }

        public override void OnPinReference(RunContext context)
        {
        }

        public override string ToString() => "gate-test-reference";
    }
}
