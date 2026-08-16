using System;
using System.IO;
using System.Linq;
using System.Text;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers writing values too large to send into the run's output.
/// </summary>
/// <remarks>
/// The behaviour under test is what a reader afterwards can rely on: that the file holds the whole
/// value, that its path means something from another machine, and that a value republished unchanged
/// does not pile up copies.
/// </remarks>
public sealed class DebugValueFileStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "tf-values-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheFileHoldsTheWholeValueRatherThanThePreview()
    {
        // The entire reason the file exists. A file that stopped at the preview budget would be a
        // second copy of what the consumer already had.
        string payload = new('x', DebugValueDescriber.PreviewBudget * 3);

        DebugValueBody body = Write("orderPayload", Text(payload))!;

        Assert.Equal(payload, File.ReadAllText(body.Path));
        Assert.Equal(payload.Length, body.SizeInBytes);
    }

    [Fact]
    public void ThePathIsAlsoStatedRelativeToTheRunSoItTravels()
    {
        // A build publishes the folder and someone reads the results on a different machine, where
        // the absolute path names a directory that does not exist.
        DebugValueBody body = Write("orderId", Text("value"))!;

        Assert.Equal("values/orderId.txt", body.RelativePath);
        Assert.True(Path.IsPathRooted(body.Path));
    }

    [Fact]
    public void RewritingTheSameContentReusesTheSameFile()
    {
        // An artifact republishes on every lifecycle change while its data stays put. Without this
        // an unchanged blob lands on disk once per transition.
        DebugValueFileStore store = NewStore();

        DebugValueBody first = store.Write("blob", Text("unchanged"))!;
        DebugValueBody second = store.Write("blob", Text("unchanged"))!;

        Assert.Equal(first.Path, second.Path);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(first.Path)!));
    }

    [Fact]
    public void ChangedContentIsANewVersionBesideTheOldOne()
    {
        // Watching a value evolve is the point of capturing versions; overwriting would leave only
        // the last one, which is the state a reader could already see.
        DebugValueFileStore store = NewStore();

        DebugValueBody first = store.Write("row", Text("before"))!;
        DebugValueBody second = store.Write("row", Text("after"))!;

        Assert.NotEqual(first.Path, second.Path);
        Assert.Equal("values/row.v2.txt", second.RelativePath);
        Assert.Equal("before", File.ReadAllText(first.Path));
        Assert.Equal("after", File.ReadAllText(second.Path));
    }

    [Fact]
    public void BinaryContentIsWrittenAsBytesRatherThanAsItsHexPreview()
    {
        // So it can be opened by whatever tool understands it. No tool understands a hex dump of a PNG.
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47];

        DebugValueBody body = Write("screenshot", new DebugValueContent(DebugPreviewForm.Binary, null, bytes, bytes.Length))!;

        Assert.Equal(bytes, File.ReadAllBytes(body.Path));
        Assert.EndsWith(".bin", body.RelativePath, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyThatIsNotAValidFileNameStillLands()
    {
        // Variable identifiers are written by people, so they carry spaces, colons and the angle
        // brackets of generic parameters.
        DebugValueBody body = Write("response<T> for /orders", Text("body"))!;

        Assert.True(File.Exists(body.Path));
        Assert.DoesNotContain('/', Path.GetFileName(body.Path));
    }

    [Fact]
    public void NothingIsWrittenUntilThereIsSomethingToWrite()
    {
        // A run that never assigns anything large must not leave an empty folder for a build to
        // publish.
        NewStore();

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void AnUnwritableOutputDirectoryTruncatesTheValueInsteadOfFailingTheRun()
    {
        // A run whose values cannot be written is not a failing run. It is a run whose values are
        // merely truncated, which is where it stood before any of this existed.
        DebugValueFileStore store = new(() => throw new IOException("The output directory is not available."));

        Assert.Null(store.Write("orderId", Text("value")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private DebugValueFileStore NewStore() => new(() => root);

    private DebugValueBody? Write(string key, DebugValueContent content) => NewStore().Write(key, content);

    private static DebugValueContent Text(string value)
        => new(DebugPreviewForm.Text, value, null, Encoding.UTF8.GetByteCount(value));
}
