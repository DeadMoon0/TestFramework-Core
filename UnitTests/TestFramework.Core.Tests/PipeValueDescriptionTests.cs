using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers a value's description surviving the trip down the pipe.
/// </summary>
/// <remarks>
/// Everything a consumer can show about a value now rides in the description. If the transport drops
/// any of it the consumer silently falls back to the one line it was sent, which looks like a working
/// UI showing an impoverished value rather than like a protocol fault.
/// </remarks>
public sealed class PipeValueDescriptionTests
{
    [Fact]
    public void EveryPartOfADescriptionArrivesOnTheOtherEnd()
    {
        PipeValueUpdateSignal sent = new()
        {
            SessionId = "session-1",
            Name = "orders",
            ValueKind = DebugValueKind.Variable,
            Envelope = new DebugValueEnvelope
            {
                Kind = DebugValueKind.Variable,
                TypeName = "System.Int32[]",
                DisplayText = "[412 items]",
                SchemaKey = DebugValueSchemaKeys.Collection,
                Description = new DebugValueDescription
                {
                    Summary = "[412 items]",
                    Shape = DebugValueShape.Collection,
                    Fields = [new DebugValueField { Name = "items", Value = "412" }],
                    Badges = ["large"],
                    Preview = new DebugValuePreview { Form = DebugPreviewForm.Json, Text = "[1,2", IsTruncated = true, SizeInBytes = 90_000 },
                    Body = new DebugValueBody
                    {
                        Path = @"C:\runs\Sample-1234abcd\values\orders.json",
                        RelativePath = "values/orders.json",
                        SizeInBytes = 90_000,
                        ContentHash = "ABCD"
                    }
                }
            }
        };

        PipeValueUpdateSignal received = RoundTrip(sent);
        DebugValueDescription described = received.Envelope.Description;

        Assert.Equal("[412 items]", described.Summary);
        Assert.Equal(DebugValueShape.Collection, described.Shape);
        Assert.Equal("items", described.Fields.Single().Name);
        Assert.Equal("412", described.Fields.Single().Value);
        Assert.Equal("large", described.Badges.Single());
        Assert.True(described.Preview!.IsTruncated);
        Assert.Equal("values/orders.json", described.Body!.RelativePath);
        Assert.Equal(90_000, described.Body.SizeInBytes);
    }

    [Fact]
    public void AnEnvelopeWithoutADescriptionStillArrives()
    {
        // A journal or a producer from before descriptions existed. It has to replay, not throw.
        PipeValueUpdateSignal sent = new()
        {
            SessionId = "session-1",
            Name = "orderId",
            ValueKind = DebugValueKind.Variable,
            Envelope = new DebugValueEnvelope
            {
                Kind = DebugValueKind.Variable,
                TypeName = "System.Int32",
                DisplayText = "42",
                SchemaKey = DebugValueSchemaKeys.Scalar
            }
        };

        PipeValueUpdateSignal received = RoundTrip(sent);

        Assert.Equal("42", received.Envelope.DisplayText);
        Assert.Equal(DebugValueShape.Unknown, received.Envelope.Description.Shape);
    }

    [Fact]
    public void ReceivingAValueDoesNotWriteItsFactsIntoTheEmptyDescription()
    {
        // The nastiest shape this can take. A deserializer that populates the value already sitting
        // on the property instead of replacing it writes into the shared Empty singleton: every
        // later value compares equal to "nothing was described", and Empty itself starts claiming
        // to be whatever arrived last. It looks like a consumer bug and is not one.
        RoundTrip(Update("orders", new DebugValueDescription
        {
            Summary = "[412 items]",
            Shape = DebugValueShape.Collection,
            Fields = [new DebugValueField { Name = "items", Value = "412" }]
        }));

        Assert.Equal(string.Empty, DebugValueDescription.Empty.Summary);
        Assert.Empty(DebugValueDescription.Empty.Fields);
        Assert.Equal(DebugValueShape.Unknown, DebugValueDescription.Empty.Shape);
    }

    [Fact]
    public void TwoValuesDoNotArriveAsTheSameDescriptionInstance()
    {
        // The other half of the same fault: everything sharing one instance means the last value to
        // arrive silently rewrites every value already on screen.
        PipeValueUpdateSignal first = RoundTrip(Update("a", new DebugValueDescription { Summary = "first" }));
        PipeValueUpdateSignal second = RoundTrip(Update("b", new DebugValueDescription { Summary = "second" }));

        Assert.NotSame(first.Envelope.Description, second.Envelope.Description);
        Assert.Equal("first", first.Envelope.Description.Summary);
        Assert.Equal("second", second.Envelope.Description.Summary);
    }

    private static PipeValueUpdateSignal Update(string name, DebugValueDescription description) => new()
    {
        SessionId = "session-1",
        Name = name,
        ValueKind = DebugValueKind.Variable,
        Envelope = new DebugValueEnvelope
        {
            Kind = DebugValueKind.Variable,
            TypeName = "System.Object",
            DisplayText = description.Summary,
            SchemaKey = DebugValueSchemaKeys.Of(description.Shape),
            Description = description
        }
    };

    /// <summary>
    /// Sends a signal through the same encoding the transport puts it through.
    /// </summary>
    /// <remarks>
    /// The wire format, not a real pipe. What can drop a property here is the serialisation, and a
    /// test that stands up two named pipes to prove it only adds ways for the test itself to hang.
    /// </remarks>
    private static PipeValueUpdateSignal RoundTrip(PipeValueUpdateSignal signal)
    {
        // Through the codec the transport actually uses, frame and all. Serialising the signal
        // directly is not the same journey — the codec wraps it in an envelope first — and testing
        // the shorter one proves the wrong thing.
        DebugEnvelope wrapped = DebugEnvelopeCodec.Deserialize(
            DebugEnvelopeCodec.Serialize(DebugEnvelopeCodec.Wrap(signal, 1)));

        return (PipeValueUpdateSignal)DebugEnvelopeCodec.Unwrap(wrapped);
    }
}
