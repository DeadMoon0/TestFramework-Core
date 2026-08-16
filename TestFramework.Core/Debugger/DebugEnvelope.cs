using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Constants describing the debugger wire protocol.
/// </summary>
public static class DebugProtocol
{
    /// <summary>
    /// Wire format version. Bump when the envelope or a payload shape changes in a way a consumer
    /// built against the previous version could misread.
    /// </summary>
    public const int Version = 2;
}

/// <summary>
/// One debugger event, in the single shape used both as a pipe frame and as a journal line.
/// </summary>
/// <remarks>
/// Keeping the two identical is the point: a completed run replayed from disk goes through exactly
/// the code a live run streams through, so there is no second parser to keep in step. The header
/// fields are outside the payload so a consumer can order, filter and version-check events without
/// deserializing the signal itself.
/// </remarks>
public sealed record DebugEnvelope
{
    /// <summary>Gets the protocol version this envelope was written with.</summary>
    public required int V { get; init; }

    /// <summary>Gets the run this event belongs to.</summary>
    public required string SessionId { get; init; }

    /// <summary>Monotonic per session, so a consumer can detect gaps and ignore duplicates.</summary>
    public required long Seq { get; init; }

    /// <summary>Gets when the event was produced.</summary>
    public required DateTimeOffset AtUtc { get; init; }

    /// <summary>Gets the discriminator for <see cref="Payload"/>.</summary>
    public required PipeSignalKind Kind { get; init; }

    /// <summary>Gets the serialized signal.</summary>
    public required JObject Payload { get; init; }
}

/// <summary>
/// Reads and writes <see cref="DebugEnvelope"/> values, both as pipe frames and as journal lines.
/// </summary>
public static class DebugEnvelopeCodec
{
    /// <summary>
    /// Gets the wire encoding.
    /// </summary>
    /// <remarks>
    /// UTF-8, not UTF-16. The previous framing spent two bytes per ASCII character for no benefit,
    /// and debug payloads are overwhelmingly ASCII JSON.
    /// </remarks>
    public static readonly Encoding WireEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Largest frame accepted, as a guard against a corrupt length prefix.</summary>
    public const int MaxMessageBytes = 4 * 1024 * 1024;

    /// <summary>Wraps a signal in an envelope carrying the given per-session sequence number.</summary>
    public static DebugEnvelope Wrap(IPipeSignal signal, long sequence)
    {
        return new DebugEnvelope
        {
            V = DebugProtocol.Version,
            SessionId = signal.SessionId,
            Seq = sequence,
            AtUtc = DateTimeOffset.UtcNow,
            Kind = signal.Kind,
            Payload = JObject.FromObject(signal)
        };
    }

    /// <summary>Serializes an envelope. This is also the journal's line format.</summary>
    public static string Serialize(DebugEnvelope envelope) => JsonConvert.SerializeObject(envelope);

    /// <summary>Reads an envelope, rejecting a protocol version this build cannot read.</summary>
    public static DebugEnvelope Deserialize(string json)
    {
        DebugEnvelope envelope = JsonConvert.DeserializeObject<DebugEnvelope>(json)
            ?? throw new FrameworkStateException("Could not deserialize debug envelope.");

        if (envelope.V != DebugProtocol.Version)
        {
            throw new FrameworkStateException(
                $"Debug protocol version mismatch: this build speaks v{DebugProtocol.Version}, the peer sent v{envelope.V}. "
                + "Producer and consumer ship together, so update whichever is older.");
        }

        return envelope;
    }

    /// <summary>Reconstructs the signal carried by an envelope.</summary>
    public static IPipeSignal Unwrap(DebugEnvelope envelope)
        => PipeSignalFactory.DeserializePayload(envelope.Kind, envelope.Payload);

    /// <summary>Length-prefixed frame: 4-byte little-endian byte count, then UTF-8 JSON.</summary>
    public static byte[] EncodeFrame(DebugEnvelope envelope)
    {
        string json = Serialize(envelope);
        int byteCount = WireEncoding.GetByteCount(json);

        if (byteCount > MaxMessageBytes)
            throw new FrameworkStateException($"Debug frame of {byteCount} bytes exceeds the {MaxMessageBytes} byte limit.");

        return [.. BitConverter.GetBytes(byteCount), .. WireEncoding.GetBytes(json)];
    }
}
