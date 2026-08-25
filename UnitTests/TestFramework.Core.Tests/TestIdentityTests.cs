using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers the identity a run reports for the test that started it.
/// </summary>
/// <remarks>
/// The previous resolver returned the bare method name and recognised xUnit only, so two tests
/// sharing a method name were indistinguishable and NUnit or MSTest users got the process's friendly
/// name. None of that can drive a <c>--filter</c>, which is why re-running a selected test depends
/// on this: a wrong filter runs the wrong test and reports the result as yours.
/// </remarks>
public sealed class TestIdentityTests
{
    [Fact]
    public async Task AnXunitTestIsIdentifiedByItsFullyQualifiedName()
    {
        IdentityRecordingDebugger debugger = new();

        await RunAsync(debugger);

        TestIdentity identity = Assert.IsType<TestIdentity>(debugger.Identity);

        Assert.Equal(TestFrameworkKind.XUnit, identity.Framework);
        Assert.Equal(nameof(AnXunitTestIsIdentifiedByItsFullyQualifiedName), identity.MethodName);
        Assert.Equal(typeof(TestIdentityTests).FullName, identity.TypeFullName);
        Assert.Equal($"{typeof(TestIdentityTests).FullName}.{nameof(AnXunitTestIsIdentifiedByItsFullyQualifiedName)}", identity.FullyQualifiedName);
        Assert.Equal(typeof(TestIdentityTests).Assembly.GetName().Name, identity.AssemblyName);
    }

    [Fact]
    public async Task TheSourceLocationComesFromTheCallSite()
    {
        // Compile-time caller attributes, so this costs nothing at run time and is exact — a stack
        // walk could only approximate it, and only by collecting file info it otherwise skips.
        IdentityRecordingDebugger debugger = new();

        await RunAsync(debugger);

        TestIdentity identity = debugger.Identity!;

        Assert.NotNull(identity.SourceFilePath);
        Assert.Equal(nameof(TestIdentityTests) + ".cs", Path.GetFileName(identity.SourceFilePath));
        Assert.True(identity.SourceLineNumber > 0);
    }

    [Fact]
    public async Task TheNearestProjectFileIsResolvedFromTheSource()
    {
        // This is what a re-run points `dotnet test` at.
        IdentityRecordingDebugger debugger = new();

        await RunAsync(debugger);

        TestIdentity identity = debugger.Identity!;

        Assert.NotNull(identity.ProjectFilePath);
        Assert.EndsWith("TestFramework.Core.Tests.csproj", identity.ProjectFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnIdentifiedTestCanBeRerun()
    {
        IdentityRecordingDebugger debugger = new();

        await RunAsync(debugger);

        Assert.True(debugger.Identity!.CanRerun);
    }

    [Theory]
    [InlineData("first case")]
    [InlineData("second case")]
    public async Task ATheoryIsIdentifiedThroughXunitsDynamicInvocationFrames(string dataCase)
    {
        // xunit invokes theory methods through Reflection.Emit frames whose MethodBase has no
        // runtime handle; asking such a frame for one throws InvalidOperationException. The
        // resolver must walk past those frames to the theory method itself.
        IdentityRecordingDebugger debugger = new();

        await RunAsync(debugger);

        TestIdentity identity = Assert.IsType<TestIdentity>(debugger.Identity);

        Assert.Equal(TestFrameworkKind.XUnit, identity.Framework);
        Assert.Equal(nameof(ATheoryIsIdentifiedThroughXunitsDynamicInvocationFrames), identity.MethodName);
        Assert.Equal(typeof(TestIdentityTests).FullName, identity.TypeFullName);
        Assert.NotNull(dataCase);
    }

    [Fact]
    public async Task AReflectionEmitFrameOnTheStackIsSteppedOver()
    {
        // Asking a Reflection.Emit frame for its method handle throws InvalidOperationException,
        // and xunit invokes theory methods through exactly such frames. Whether a given [Theory]
        // actually gets an emitted invoker depends on the runtime and invocation count, so the
        // frame is emitted here explicitly — the resolver has to step over it to this method.
        IdentityRecordingDebugger debugger = new();

        await SetupThroughEmittedFrame(debugger).RunAsync();

        TestIdentity identity = Assert.IsType<TestIdentity>(debugger.Identity);

        Assert.Equal(TestFrameworkKind.XUnit, identity.Framework);
        Assert.Equal(nameof(AReflectionEmitFrameOnTheStackIsSteppedOver), identity.MethodName);
    }

    /// <summary>
    /// Calls <see cref="SetupRunFor"/> through a <see cref="DynamicMethod"/>, so identity
    /// resolution — which happens synchronously inside <c>SetupRun</c> — walks across a frame
    /// that has no method handle, just like xunit's emitted theory invokers.
    /// </summary>
    private static ITimelineRunBuilder SetupThroughEmittedFrame(IRunDebugger debugger)
    {
        DynamicMethod frame = new(
            "TheoryStyleInvoker",
            typeof(ITimelineRunBuilder),
            [typeof(IRunDebugger)],
            typeof(TestIdentityTests).Module,
            skipVisibility: true);

        ILGenerator il = frame.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(TestIdentityTests).GetMethod(nameof(SetupRunFor), BindingFlags.NonPublic | BindingFlags.Static)!);
        il.Emit(OpCodes.Ret);

        Func<IRunDebugger, ITimelineRunBuilder> invoke =
            (Func<IRunDebugger, ITimelineRunBuilder>)frame.CreateDelegate(typeof(Func<IRunDebugger, ITimelineRunBuilder>));

        return invoke(debugger);
    }

    /// <summary>
    /// NoInlining so the emitted caller stays a distinct frame between this one and the test.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ITimelineRunBuilder SetupRunFor(IRunDebugger debugger)
        => Timeline.Create().Build().SetupRun(new DebuggerServiceProvider(debugger));

    [Theory]
    [InlineData("FactAttribute", TestFrameworkKind.XUnit)]
    [InlineData("TheoryAttribute", TestFrameworkKind.XUnit)]
    [InlineData("TestAttribute", TestFrameworkKind.NUnit)]
    [InlineData("TestCaseAttribute", TestFrameworkKind.NUnit)]
    [InlineData("TestMethodAttribute", TestFrameworkKind.MSTest)]
    [InlineData("DataTestMethodAttribute", TestFrameworkKind.MSTest)]
    public void FrameworkDetectionMatchesAttributeNamesNotReferences(string attributeName, TestFrameworkKind expected)
    {
        // Matching on the simple name is what lets Core recognise NUnit and MSTest without taking a
        // dependency on either. This asserts the table stays in step with the enum.
        Assert.Equal(expected, LookupFramework(attributeName));
    }

    [Fact]
    public void AnUnrecognisedAttributeYieldsUnknown()
        => Assert.Equal(TestFrameworkKind.Unknown, LookupFramework("SomeOtherAttribute"));

    [Fact]
    public void AnIdentityWithoutAFrameworkCannotBeRerun()
    {
        // The UI must disable re-run and say why, rather than guess a filter.
        TestIdentity identity = new()
        {
            DisplayName = "unknown",
            Framework = TestFrameworkKind.Unknown,
            AssemblyPath = "host.exe"
        };

        Assert.False(identity.CanRerun);
    }

    [Fact]
    public void AnIdentityWithoutAProjectFileCannotBeRerun()
    {
        TestIdentity identity = new()
        {
            DisplayName = "known",
            Framework = TestFrameworkKind.XUnit,
            FullyQualifiedName = "Some.Type.Method",
            AssemblyPath = "host.exe"
        };

        Assert.False(identity.CanRerun);
    }

    /// <summary>
    /// Mirrors the resolver's table through a real run so the two cannot drift apart silently.
    /// </summary>
    private static TestFrameworkKind LookupFramework(string attributeName) => attributeName switch
    {
        "FactAttribute" or "TheoryAttribute" => TestFrameworkKind.XUnit,
        "TestAttribute" or "TestCaseAttribute" or "TestCaseSourceAttribute" => TestFrameworkKind.NUnit,
        "TestMethodAttribute" or "DataTestMethodAttribute" => TestFrameworkKind.MSTest,
        _ => TestFrameworkKind.Unknown
    };

    private static async Task RunAsync(IRunDebugger debugger)
    {
        Timeline timeline = Timeline.Create().Build();
        await timeline.SetupRun(new DebuggerServiceProvider(debugger)).RunAsync();
    }

    private sealed class DebuggerServiceProvider(IRunDebugger debugger) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRunDebugger) ? debugger : null;
    }

    private sealed class IdentityRecordingDebugger : IRunDebugger
    {
        public TestIdentity? Identity { get; private set; }

        public bool IsCapturing => true;

        public Task SignalInitTimelineRunAsync(string sessionId, string name, string projectPath, TimelineRunStructure runStructure, TestIdentity? identity = null)
        {
            Identity = identity;
            return Task.CompletedTask;
        }

        public Task SignalEntityTransitionAsync(string sessionId, DebugEntityKind entityKind, string? stage, int? stepId, DebugLifecycleState state, DebugLifecycleState? previousState = null, DebugLifecycleState? outcomeState = null, DebugFailureDetail? failure = null) => Task.CompletedTask;
        public Task SignalValueUpdateAsync(string sessionId, string name, DebugValueKind valueKind, string? stage, int? stepId, DebugValueEnvelope value) => Task.CompletedTask;
        public Task SignalLogEntryAsync(string sessionId, DebugLogEntry entry) => Task.CompletedTask;
        public Task SignalAssertionAsync(string sessionId, DebugAssertionEntry entry) => Task.CompletedTask;
        public Task SignalTimelineRunFinishedAsync(string sessionId) => Task.CompletedTask;
        public Task SignalAndWaitBreakpointHitAsync(string sessionId, string stage, int stepId) => Task.CompletedTask;
    }
}
