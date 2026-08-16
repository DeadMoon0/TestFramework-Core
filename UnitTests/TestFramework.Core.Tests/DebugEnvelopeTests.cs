using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Tests;

public sealed class DebugEnvelopeTests
{
    [Fact]
    public void WrapCarriesTheHeaderWithoutTouchingThePayload()
    {
        PipeTimelineRunFinishedSignal signal = new() { SessionId = "session-7" };

        DebugEnvelope envelope = DebugEnvelopeCodec.Wrap(signal, 42);

        Assert.Equal(DebugProtocol.Version, envelope.V);
        Assert.Equal("session-7", envelope.SessionId);
        Assert.Equal(42, envelope.Seq);
        Assert.Equal(PipeSignalKind.TimelineRunFinished, envelope.Kind);
        Assert.NotNull(envelope.Payload);
    }

    [Fact]
    public void EnvelopeRoundTripsThroughItsSerializedForm()
    {
        PipeBreakpointHitRequestSignal signal = new() { SessionId = "session-1", Stage = "Main", StepId = 3 };

        DebugEnvelope original = DebugEnvelopeCodec.Wrap(signal, 1);
        DebugEnvelope restored = DebugEnvelopeCodec.Deserialize(DebugEnvelopeCodec.Serialize(original));

        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.Equal(original.Seq, restored.Seq);
        Assert.Equal(original.Kind, restored.Kind);

        PipeBreakpointHitRequestSignal unwrapped = Assert.IsType<PipeBreakpointHitRequestSignal>(DebugEnvelopeCodec.Unwrap(restored));
        Assert.Equal("Main", unwrapped.Stage);
        Assert.Equal(3, unwrapped.StepId);
    }

    [Fact]
    public void FrameIsLengthPrefixedUtf8()
    {
        PipeTimelineRunFinishedSignal signal = new() { SessionId = "s" };
        DebugEnvelope envelope = DebugEnvelopeCodec.Wrap(signal, 1);

        byte[] frame = DebugEnvelopeCodec.EncodeFrame(envelope);
        int declaredLength = BitConverter.ToInt32(frame, 0);

        Assert.Equal(frame.Length - sizeof(int), declaredLength);

        string json = DebugEnvelopeCodec.WireEncoding.GetString(frame, sizeof(int), declaredLength);
        Assert.Equal(DebugEnvelopeCodec.Serialize(envelope), json);

        // The payload here is pure ASCII. Under the previous UTF-16 framing the byte count would be
        // double the character count; this asserts the halving is real and not just declared.
        Assert.Equal(json.Length, declaredLength);
        Assert.Equal(json.Length * 2, Encoding.Unicode.GetByteCount(json));
    }

    [Fact]
    public void VersionMismatchIsRejectedWithAnActionableMessage()
    {
        DebugEnvelope envelope = DebugEnvelopeCodec.Wrap(new PipeTimelineRunFinishedSignal { SessionId = "s" }, 1);
        JObject raw = JObject.Parse(DebugEnvelopeCodec.Serialize(envelope));
        raw["V"] = DebugProtocol.Version + 1;

        FrameworkStateException exception = Assert.Throws<FrameworkStateException>(
            () => DebugEnvelopeCodec.Deserialize(raw.ToString(Formatting.None)));

        Assert.Contains($"v{DebugProtocol.Version}", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"v{DebugProtocol.Version + 1}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedFrameIsRefusedRatherThanTruncated()
    {
        DebugEnvelope oversized = new()
        {
            V = DebugProtocol.Version,
            SessionId = "s",
            Seq = 1,
            AtUtc = DateTimeOffset.UnixEpoch,
            Kind = PipeSignalKind.TimelineRunFinished,
            Payload = new JObject { ["blob"] = new string('x', DebugEnvelopeCodec.MaxMessageBytes + 1) }
        };

        Assert.Throws<FrameworkStateException>(() => DebugEnvelopeCodec.EncodeFrame(oversized));
    }

    [Fact]
    public void SerializedEnvelopeIsAlsoTheJournalLine()
    {
        // One shape for both transports is what lets a replayed run take the live code path.
        DebugEnvelope envelope = DebugEnvelopeCodec.Wrap(new PipeTimelineRunFinishedSignal { SessionId = "s" }, 5);

        string line = DebugEnvelopeCodec.Serialize(envelope);
        byte[] frame = DebugEnvelopeCodec.EncodeFrame(envelope);
        string framed = DebugEnvelopeCodec.WireEncoding.GetString(frame, sizeof(int), frame.Length - sizeof(int));

        Assert.Equal(line, framed);
        Assert.DoesNotContain('\n', line);
    }
}
