using System;
using Newtonsoft.Json;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Rebuilds protocol messages from their serialized form.
/// </summary>
public static class PipeSignalFactory
{
    /// <summary>
    /// Rebuilds a signal from an envelope payload.
    /// </summary>
    /// <remarks>
    /// The discriminator stays an explicit switch on <see cref="PipeSignalKind"/> rather than
    /// reflection-driven polymorphism: this deserializes data arriving over a local socket, and
    /// letting the payload name its own CLR type would be a deserialization hole.
    /// </remarks>
    public static IPipeSignal DeserializePayload(PipeSignalKind kind, Newtonsoft.Json.Linq.JObject payload)
        => DeserializeSignal(kind, payload.ToString(Formatting.None));

    /// <summary>
    /// Rebuilds a signal from its serialized form, reading the discriminator out of the JSON.
    /// </summary>
    public static IPipeSignal DeserializeSignal(string json)
    {
        PipeSignalKind signalKind = (JsonConvert.DeserializeAnonymousType(json, new { Kind = (PipeSignalKind)0 }, DebugJson.Settings)
            ?? throw new FrameworkStateException("Could not deserialize pipe signal."))
            .Kind;

        return DeserializeSignal(signalKind, json);
    }

    private static IPipeSignal DeserializeSignal(PipeSignalKind signalKind, string json)
    {
        return signalKind switch
        {
            PipeSignalKind.InitTimelineRun => JsonConvert.DeserializeObject<PipeInitTimelineRunSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize init signal."),
            PipeSignalKind.EntityTransition => JsonConvert.DeserializeObject<PipeEntityTransitionSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize entity transition signal."),
            PipeSignalKind.ValueUpdate => JsonConvert.DeserializeObject<PipeValueUpdateSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize value update signal."),
            PipeSignalKind.LogEntry => JsonConvert.DeserializeObject<PipeLogEntrySignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize log entry signal."),
            PipeSignalKind.Assertion => JsonConvert.DeserializeObject<PipeAssertionSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize assertion signal."),
            PipeSignalKind.BreakpointHitRequest => JsonConvert.DeserializeObject<PipeBreakpointHitRequestSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize breakpoint request signal."),
            PipeSignalKind.BreakpointHitContinue => JsonConvert.DeserializeObject<PipeBreakpointHitContinueSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize breakpoint continue signal."),
            PipeSignalKind.TimelineRunFinished => JsonConvert.DeserializeObject<PipeTimelineRunFinishedSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize timeline-finished signal."),
            PipeSignalKind.CancelRun => JsonConvert.DeserializeObject<PipeCancelRunSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize cancel-run signal."),
            PipeSignalKind.Widget => JsonConvert.DeserializeObject<PipeWidgetSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize widget signal."),
            PipeSignalKind.CaptureWidgetRequest => JsonConvert.DeserializeObject<PipeCaptureWidgetRequestSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize widget capture request."),
            PipeSignalKind.CaptureWidgetAck => JsonConvert.DeserializeObject<PipeCaptureWidgetAckSignal>(json, DebugJson.Settings) ?? throw new FrameworkStateException("Could not deserialize widget capture acknowledgement."),
            _ => throw new ArgumentOutOfRangeException(nameof(signalKind), signalKind, "Unsupported pipe signal kind.")
        };
    }
}
