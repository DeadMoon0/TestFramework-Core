using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers the two promises the protocol makes about how it reads.
/// </summary>
/// <remarks>
/// A journal is a file people open, so an enum has to arrive as a name. And a plan has to state the policies a
/// step runs under, because for several releases it shipped the builder objects holding them and none of the
/// numbers inside.
/// </remarks>
public class ProtocolReadabilityTests
{
    [Fact]
    public void EveryEnumOnTheWireArrivesAsItsName()
    {
        PipeEntityTransitionSignal signal = new()
        {
            SessionId = "session-1",
            EntityKind = DebugEntityKind.Step,
            Stage = "Main",
            StepId = 0,
            State = DebugLifecycleState.Timeout,
            PreviousState = DebugLifecycleState.Running
        };

        string json = DebugEnvelopeCodec.Serialize(DebugEnvelopeCodec.Wrap(signal, sequence: 1));

        // Readable without a copy of this assembly beside the file.
        Assert.Contains("\"Kind\": \"EntityTransition\"".Replace(": ", ":", StringComparison.Ordinal), json, StringComparison.Ordinal);
        Assert.Contains("\"State\":\"Timeout\"", json, StringComparison.Ordinal);
        Assert.Contains("\"EntityKind\":\"Step\"", json, StringComparison.Ordinal);

        // And it comes back as the enum it was.
        DebugEnvelope envelope = DebugEnvelopeCodec.Deserialize(json);
        PipeEntityTransitionSignal restored = Assert.IsType<PipeEntityTransitionSignal>(DebugEnvelopeCodec.Unwrap(envelope));

        Assert.Equal(DebugLifecycleState.Timeout, restored.State);
        Assert.Equal(DebugEntityKind.Step, restored.EntityKind);
    }

    [Fact]
    public void ANumberedEnumWouldHaveBeenAmbiguousAcrossVersions()
    {
        // The other reason for names: inserting a member ahead of another one renumbers everything after it,
        // and a recording made before the insert would then read as the wrong state.
        Assert.Equal(
            "\"Skipped\"",
            JsonConvert.SerializeObject(DebugLifecycleState.Skipped, DebugJson.Settings));
    }

    [Fact]
    public void APlanStatesTheRetryCountAndTheTimeout()
    {
        DebugStepState step = new()
        {
            Name = "Fetch",
            Description = "Reads the order",
            Phase = StepExecutionPhase.Act,
            DoesReturn = true,
            Parallelization = StepParallelizationMode.Parallelizable,
            MaxRetries = 3,
            TimeOut = TimeSpan.FromSeconds(30),
            IgnoredExceptions = ["HttpRequestException"],
            Inputs = [new DebugStepIo { Key = "orderId", Kind = StepIOKind.Variable, DeclaredType = nameof(Int32) }]
        };

        // Asserted against the text, because the text is what a reader opens.
        string written = JsonConvert.SerializeObject(step, DebugJson.Settings);

        Assert.Contains("\"MaxRetries\":3", written, StringComparison.Ordinal);
        Assert.Contains("\"TimeOut\":\"00:00:30\"", written, StringComparison.Ordinal);
        Assert.Contains("\"IgnoredExceptions\":[\"HttpRequestException\"]", written, StringComparison.Ordinal);
        Assert.Contains("\"Phase\":\"Act\"", written, StringComparison.Ordinal);
        Assert.Contains("\"Parallelization\":\"Parallelizable\"", written, StringComparison.Ordinal);
        Assert.Contains("\"DeclaredType\":\"Int32\"", written, StringComparison.Ordinal);

        // And nothing about the builder that held any of it.
        Assert.DoesNotContain("IsFrozen", written, StringComparison.Ordinal);
        Assert.DoesNotContain("RequireImmutability", written, StringComparison.Ordinal);
    }

    [Fact]
    public void APolicyPinnedToAVariableNamesTheVariableRatherThanGuessingAValue()
    {
        // At the moment a plan is sent, a variable the retry count reads from may not have been written yet.
        // Naming it says so; reporting a number would be a guess presented as a policy.
        DebugStepState step = new()
        {
            Name = "Fetch",
            Description = string.Empty,
            Phase = StepExecutionPhase.Act,
            DoesReturn = false,
            Parallelization = StepParallelizationMode.Parallelizable,
            MaxRetriesVariable = "retryBudget"
        };

        Assert.Null(step.MaxRetries);
        Assert.Equal("retryBudget", step.MaxRetriesVariable);
    }

    [Fact]
    public void ARenderingOfAFactNeverTravelsBesideTheFact()
    {
        // Both records offer a text form of their value for a consumer that only wants to print one. Offering it
        // is fine; shipping it is the habit this protocol was cleaned up to break, and a public getter is
        // serialized by default.
        string field = JsonConvert.SerializeObject(DebugLogField.Of("count", 4000), DebugJson.Settings);
        string fact = JsonConvert.SerializeObject(
            new DebugValueField { Name = "length", Value = new JValue(5) },
            DebugJson.Settings);

        Assert.Equal("{\"Name\":\"count\",\"Value\":4000}", field);
        Assert.DoesNotContain("Text", fact, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInnerExceptionChainKeepsItsTypesApartFromItsMessages()
    {
        DebugFailureDetail failure = DebugFailureDetail.Capture(
            new InvalidOperationException("outer", new FormatException("time: 3:00 is not a time")),
            attempt: 1,
            willRetry: false,
            wasSuppressed: false)!;

        DebugExceptionLink link = Assert.Single(failure.InnerExceptions);

        // The message contains a colon, which is exactly why joining the two into one string was a trap for
        // anyone trying to split them back apart.
        Assert.Equal("System.FormatException", link.ExceptionType);
        Assert.Equal("time: 3:00 is not a time", link.Message);
    }
}
