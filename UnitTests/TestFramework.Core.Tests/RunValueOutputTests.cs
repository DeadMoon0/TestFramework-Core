using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers a run writing its oversized values into its own output and shipping only a reference.
/// </summary>
/// <remarks>
/// The arrangement this replaces gave a reader two bad options: a value cut to a few thousand
/// characters, which loses the part that mattered, or the whole thing through every update, which
/// makes the debug protocol grow with whatever a run happens to assign.
/// </remarks>
public sealed class RunValueOutputTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "tf-run-output-" + Guid.NewGuid().ToString("N"));
    private readonly string? previous = System.Environment.GetEnvironmentVariable(RunOutput.DirectoryVariable);

    public RunValueOutputTests()
    {
        System.Environment.SetEnvironmentVariable(RunOutput.DirectoryVariable, root);
    }

    [Fact]
    public async Task ALargeVariableIsWrittenToTheRunOutputAndReferencedByPath()
    {
        string payload = new('x', DebugValueDescriber.PreviewBudget * 2);
        ValueRecordingDebugger debugger = new();

        await Run(debugger, "payload", payload);

        DebugValueEnvelope envelope = debugger.Envelopes["payload"];

        Assert.NotNull(envelope.Description.Body);
        Assert.Equal("values/payload.txt", envelope.Description.Body!.RelativePath);
        Assert.Equal(payload, File.ReadAllText(envelope.Description.Body.Path));
    }

    [Fact]
    public async Task TheReferenceAppearsExactlyWhenThePreviewCouldNotCarryTheValue()
    {
        // Two ways of asking one question. If they could disagree, a consumer would end up showing a
        // truncated value with no way to reach the rest of it.
        ValueRecordingDebugger debugger = new();

        await Run(debugger, "small", "a short value");

        DebugValueEnvelope envelope = debugger.Envelopes["small"];

        Assert.False(envelope.Description.Preview!.IsTruncated);
        Assert.Null(envelope.Description.Body);
    }

    [Fact]
    public async Task ARunThatAssignsNothingLargeWritesNoFilesAtAll()
    {
        // A build publishes this folder. Runs that had nothing to say must not litter it.
        await Run(new ValueRecordingDebugger(), "small", "a short value");

        Assert.False(Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task TheRunFolderIsNamedAfterTheTestRatherThanOnlyByAnIdentifier()
    {
        // These folders are browsed by people looking for the run that failed.
        ValueRecordingDebugger debugger = new();

        await Run(debugger, "payload", new string('x', DebugValueDescriber.PreviewBudget * 2));

        string folder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(debugger.Envelopes["payload"].Description.Body!.Path))!);

        // Named, then made unique. The suffix is what keeps parallel runs on one agent apart; without
        // the name in front of it the folder is a GUID nobody can match to a failure.
        Assert.StartsWith(nameof(TheRunFolderIsNamedAfterTheTestRatherThanOnlyByAnIdentifier), folder, StringComparison.Ordinal);
        Assert.Matches("-[0-9a-f]{8}$", folder);
    }

    private static async Task Run(IRunDebugger debugger, string name, string value)
    {
        Timeline timeline = Timeline.Create()
            .SetVariable(name, Var.Const(value))
            .Build();

        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(RunOutput.DirectoryVariable, previous);

        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class ValueRecordingDebugger : IRunDebugger
    {
        private readonly Dictionary<string, DebugValueEnvelope> envelopes = [];

        internal IReadOnlyDictionary<string, DebugValueEnvelope> Envelopes => envelopes;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null) => Task.CompletedTask;

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;

        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value)
        {
            envelopes[name] = value;
            return Task.CompletedTask;
        }

        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;

        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;

        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;

        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
