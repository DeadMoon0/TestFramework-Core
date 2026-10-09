using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// A variable holding a credential: the run and the test use it, and nothing that describes it shows it.
/// </summary>
/// <remarks>
/// The rule comes from the family's one-channel rule for secrets: a session cookie read into a variable was the one
/// road a captured credential could still take into the debugger's variable view, its value files and a failed
/// assertion's message, in clear.
/// </remarks>
public class SecretVariableTests
{
    private const string Token = "s3cr3t-session-token";

    [Fact]
    public async Task TheTestStillComparesTheSecretItself()
    {
        TimelineRun run = await Run(new StoresSecret(Token));

        run.EnsureRanToCompletion();
        run.Variable<string>("token").Should().Be(Token);
    }

    [Fact]
    public async Task NothingTheDebuggerReceivesCarriesTheSecret()
    {
        RecordingDebugger debugger = new();
        TimelineRun run = await Run(new StoresSecret(Token), debugger);

        run.Variable<string>("token").Should().Be(Token);

        Assert.NotEmpty(debugger.Sent);
        Assert.DoesNotContain(debugger.Sent, sent => sent.Contains(Token, StringComparison.Ordinal));
        Assert.Contains(debugger.Sent, sent => sent.Contains("(secret)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailedAssertionSaysItFailedWithoutQuotingEitherValue()
    {
        TimelineRun run = await Run(new StoresSecret(Token));

        ValueAssertionException failure = Assert.Throws<ValueAssertionException>(() => run.Variable<string>("token").Should().Be("a-guessed-token"));

        Assert.DoesNotContain(Token, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("a-guessed-token", failure.Message, StringComparison.Ordinal);
        Assert.Contains("secret", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlainWriteUnderASecretName_StaysSecret()
    {
        RecordingDebugger debugger = new();
        TimelineRun run = await Run(new StoresSecret(Token, thenPlain: "a-refreshed-token"), debugger);

        run.EnsureRanToCompletion();
        Assert.DoesNotContain(debugger.Sent, sent => sent.Contains("a-refreshed-token", StringComparison.Ordinal));
        Assert.True(run.VariableStore.IsSecret("token"));
    }

    [Fact]
    public async Task AnOrdinaryVariableIsStillShown()
    {
        // The other half: redaction is for what was written as secret, not for every variable.
        RecordingDebugger debugger = new();
        await Run(new StoresSecret(Token), debugger, plain: "visible-value");

        Assert.Contains(debugger.Sent, sent => sent.Contains("visible-value", StringComparison.Ordinal));
    }

    private static async Task<TimelineRun> Run(StoresSecret step, IRunDebugger? debugger = null, string? plain = null)
    {
        ITimelineBuilder builder = Timeline.Create();
        if (plain is not null)
            builder.SetVariable("plain", Var.Const(plain));

        Timeline timeline = builder.Trigger(step).Build();

        return debugger is null
            ? await timeline.SetupRun().RunAsync()
            : await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();
    }

    private sealed class StoresSecret(string secret, string? thenPlain = null) : Step<EmptyStepResultContext>
    {
        public override string Name => "Stores a secret";

        public override string Description => "Reads a credential into a variable.";

        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new StoresSecret(secret, thenPlain).WithClonedOptions(this);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract) => contract.Output(new VariableIdentifier("token"), typeof(string));

        public override Task<EmptyStepResultContext?> Execute(RunContext context)
        {
            context.Variables.SetSecretVariable("token", secret);
            if (thenPlain is not null)
                context.Variables.SetVariable("token", thenPlain);

            return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
        }
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    /// <summary>Everything the run sends a debugger about values and assertions, as the text that goes over the wire.</summary>
    private sealed class RecordingDebugger : IRunDebugger
    {
        private readonly List<string> sent = [];

        public IReadOnlyList<string> Sent
        {
            get
            {
                lock (sent) return [.. sent];
            }
        }

        public bool IsCapturing => true;

        public Task SignalValueUpdateAsync(string sessionId, DebugValueUpdate update) => Record(update);

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Record(entry);

        public Task SignalInitTimelineRunAsync(string sessionId, DebugRunStart start) => Record(start);

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityTransition transition) => Task.CompletedTask;

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Record(entry);

        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;

        private Task Record(object payload)
        {
            lock (sent) sent.Add(JsonConvert.SerializeObject(payload));
            return Task.CompletedTask;
        }
    }
}
