using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers describing a value as facts rather than as a formatted line.
/// </summary>
/// <remarks>
/// The point of the change is that the producer stops deciding the presentation. These tests are
/// therefore about what is <em>stated</em> — the summary, the named facts, whether content was cut —
/// and never about how any of it is spaced or punctuated.
/// </remarks>
public sealed class DebugValueDescriptionTests
{
    [Fact]
    public void ACollectionIsSummarisedByItsSizeRatherThanItsContents()
    {
        // "[412 items]" tells a reader more about a large collection than its first four entries do,
        // and the entries are in the preview for anyone who wants them.
        DebugValueDescription described = Describe(Enumerable.Range(0, 412).ToArray());

        Assert.Equal("[412 items]", described.Summary);
        Assert.Equal("412", Fact(described, "items"));
    }

    [Fact]
    public void ADictionaryCountsItsEntries()
    {
        DebugValueDescription described = Describe(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

        Assert.Equal("{2 entries}", described.Summary);
        Assert.Equal("2", Fact(described, "entries"));
    }

    [Fact]
    public void AStringCarriesItsLengthAndItsContent()
    {
        DebugValueDescription described = Describe("Ada");

        Assert.Equal("3", Fact(described, "length"));
        Assert.Equal(DebugPreviewForm.Text, described.Preview!.Form);
        Assert.Equal("Ada", described.Preview.Text);
        Assert.False(described.Preview.IsTruncated);
    }

    [Fact]
    public void CuttingContentIsStatedRatherThanImplied()
    {
        // An ellipsis leaves the reader unsure whether the value really ends in three dots. A
        // consumer that knows it was cut can offer the rest instead.
        DebugValueDescription described = Describe(new string('x', DebugValueDescriber.PreviewBudget + 50));

        Assert.True(described.Preview!.IsTruncated);
        Assert.Equal(DebugValueDescriber.PreviewBudget, described.Preview.Text.Length);
        Assert.Equal(DebugValueDescriber.PreviewBudget + 50, described.Preview.SizeInBytes);
    }

    [Fact]
    public void ASummaryIsOneLineEvenWhenTheValueIsNot()
    {
        // A consumer shows the summary where it has room for exactly one line. Left as-is, a
        // multi-line string does not truncate there — it wraps, and one value takes seven rows of a
        // rail sized for one. The line breaks survive in the preview, which is where they belong.
        DebugValueDescription described = Describe("first line\r\nsecond line\r\nthird line");

        Assert.DoesNotContain('\n', described.Summary);
        Assert.DoesNotContain('\r', described.Summary);
        Assert.Contains("first line second line", described.Summary, StringComparison.Ordinal);
        Assert.Contains("\r\n", described.Preview!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObjectIsPreviewedAsJson()
    {
        DebugValueDescription described = Describe(new { Name = "Ada", Age = 36 });

        Assert.Equal(DebugPreviewForm.Json, described.Preview!.Form);
        Assert.Contains("Ada", described.Preview.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AScalarNeedsNoPreview()
    {
        // Its preview would only repeat its summary, and it rides along on every single update.
        Assert.Null(Describe(42).Preview);
        Assert.Null(Describe(true).Preview);
        Assert.Null(Describe(Guid.Empty).Preview);
    }

    [Fact]
    public void BytesAreDescribedAsBytes()
    {
        DebugValueDescription described = Describe(new byte[] { 1, 2, 3 });

        Assert.Equal("3 bytes", described.Summary);
        Assert.Equal(DebugPreviewForm.Binary, described.Preview!.Form);
        Assert.Equal("010203", described.Preview.Text);
    }

    [Fact]
    public void NullIsAValueLikeAnyOther()
    {
        DebugValueDescription described = Describe(null);

        Assert.Equal("<null>", described.Summary);
        Assert.Empty(described.Fields);
        Assert.Null(described.Preview);
    }

    [Fact]
    public void AGenericTypeIsNamedReadably()
    {
        DebugValueDescription described = Describe(new List<string> { "a" });

        Assert.Equal("List<String>", Fact(described, "type"));
    }

    [Fact]
    public void TwoValuesDifferingOnlyPastTheDisplayCutOffAreStillDifferent()
    {
        // The reason the token is taken over the untruncated form. Getting this wrong drops the
        // second write silently, on exactly the large payloads someone is most likely inspecting.
        string left = new string('x', 200) + "left";
        string right = new string('x', 200) + "right";

        Assert.NotEqual(DebugValueDescriber.Describe(left).ChangeToken, DebugValueDescriber.Describe(right).ChangeToken);
    }

    [Fact]
    public void TheSameValueDescribedTwiceHasTheSameToken()
    {
        Assert.Equal(
            DebugValueDescriber.Describe(new { Name = "Ada" }).ChangeToken,
            DebugValueDescriber.Describe(new { Name = "Ada" }).ChangeToken);
    }

    [Fact]
    public void AValueThatCannotBeSerialisedIsStillDescribedAndStillDistinguishable()
    {
        // A debug payload that throws must not take the run down, and a constant token here would
        // make every such value look unchanged forever.
        DescribedValue first = DebugValueDescriber.Describe(new Awkward("one"));
        DescribedValue second = DebugValueDescriber.Describe(new Awkward("two"));

        Assert.NotEqual(first.ChangeToken, second.ChangeToken);
        Assert.NotEmpty(first.Description.Summary);
    }

    [Theory]
    [InlineData(null, DebugValueShape.Null)]
    [InlineData(42, DebugValueShape.Scalar)]
    [InlineData(true, DebugValueShape.Scalar)]
    [InlineData("Ada", DebugValueShape.Text)]
    public void AValueKnowsWhatShapeItIs(object? value, DebugValueShape expected)
    {
        Assert.Equal(expected, Describe(value).Shape);
    }

    [Fact]
    public void TheShapesThatOverlapAreResolvedInFavourOfTheNarrowerOne()
    {
        // A string is a sequence of characters and a byte array is a collection, so both would be
        // claimed by the sequence case if it were reached first. Getting this wrong sends every
        // string in every run to the list renderer.
        Assert.Equal(DebugValueShape.Text, Describe("Ada").Shape);
        Assert.Equal(DebugValueShape.Binary, Describe(new byte[] { 1, 2 }).Shape);
        Assert.Equal(DebugValueShape.Dictionary, Describe(new Dictionary<string, int>()).Shape);
        Assert.Equal(DebugValueShape.Collection, Describe(new[] { 1, 2 }).Shape);
        Assert.Equal(DebugValueShape.Object, Describe(new { Name = "Ada" }).Shape);
    }

    [Fact]
    public void EveryShapeHasItsOwnSchemaKey()
    {
        // A consumer registers renderers against these, so two shapes sharing a key would silently
        // route one of them to the wrong renderer.
        DebugValueShape[] shapes = Enum.GetValues<DebugValueShape>();

        Assert.Equal(shapes.Length, shapes.Select(DebugValueSchemaKeys.Of).Distinct().Count());
        Assert.All(shapes, shape => Assert.StartsWith("tf.value.", DebugValueSchemaKeys.Of(shape), StringComparison.Ordinal));
    }

    [Fact]
    public void AValueReplayedWithoutAShapeIsSaidToBeUnknownRatherThanScalar()
    {
        // A journal recorded before shapes existed replays with the default. It has to land on a key
        // that means "no idea", not on one that claims the value is something it may not be.
        DebugValueDescription older = new() { Summary = "whatever an older version wrote" };

        Assert.Equal(DebugValueShape.Unknown, older.Shape);
        Assert.Equal(DebugValueSchemaKeys.Unknown, DebugValueSchemaKeys.Of(older.Shape));
    }

    private static DebugValueDescription Describe(object? value) => DebugValueDescriber.Describe(value).Description;

    private static string? Fact(DebugValueDescription described, string name)
        => described.Fields.FirstOrDefault(field => field.Name == name)?.Value;

    /// <summary>A value whose serialisation throws, which is a thing that happens in real runs.</summary>
    private sealed class Awkward(string tag)
    {
        public string Boom => throw new InvalidOperationException("This property cannot be read.");

        public override string ToString() => tag;
    }
}
