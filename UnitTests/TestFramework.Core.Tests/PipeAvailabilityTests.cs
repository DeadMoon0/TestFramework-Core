using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers the availability probe that decides, per run, whether a debugger UI is listening.
/// </summary>
/// <remarks>
/// These run on every platform. The probe reads the Windows object namespace on Windows and the
/// socket file backing the pipe under the temp directory on Unix, so a negative result is available
/// on both -- which matters, because answering "possibly listening" where it is unknown costs a
/// connect attempt on every run of every suite on a machine that has no UI at all.
///
/// Keeping them cross-platform is also what pins the Unix socket path convention: it is an
/// implementation detail of System.IO.Pipes, and if it ever changed these fail rather than the
/// feature quietly ceasing to find a UI.
/// </remarks>
public sealed class PipeAvailabilityTests : IDisposable
{
    private readonly string pipeName = "TestFrameworkDebugTests_" + Guid.NewGuid().ToString("N");

    public void Dispose() => PipeClient.ResetAvailabilityForTests();

    [Fact]
    public void ProbeReportsNoListenerForAnUnusedPipeName()
    {
        PipeClient.ResetAvailabilityForTests();

        Assert.False(PipeAvailability.IsListening(pipeName));
    }

    [Fact]
    public void ProbeReportsAListenerWhileAServerIsOpen()
    {
        using NamedPipeServerStream server = CreateServer();
        PipeClient.ResetAvailabilityForTests();

        Assert.True(PipeAvailability.IsListening(pipeName));
    }

    [Fact]
    public void ProbeNoticesAUiThatStartsLater()
    {
        // The behaviour the removed latch used to prevent: a suite that starts with no UI must pick
        // one up when it appears, without an env var and without restarting the test host.
        PipeClient.ResetAvailabilityForTests();
        Assert.False(PipeAvailability.IsListening(pipeName));

        using NamedPipeServerStream server = CreateServer();
        PipeClient.ResetAvailabilityForTests();

        Assert.True(PipeAvailability.IsListening(pipeName));
    }

    [Fact]
    public void ProbeNoticesAUiThatGoesAway()
    {
        NamedPipeServerStream server = CreateServer();
        PipeClient.ResetAvailabilityForTests();
        Assert.True(PipeAvailability.IsListening(pipeName));

        server.Dispose();
        PipeClient.ResetAvailabilityForTests();

        Assert.False(PipeAvailability.IsListening(pipeName));
    }

    [Fact]
    public void RepeatedProbesAreServedFromTheCache()
    {
        // IsCapturing is consulted on every variable write, so the probe must not become a syscall
        // per write. Correctness of the cached answer is what the tests above cover; this one only
        // asserts the answer stays stable within the window.
        PipeClient.ResetAvailabilityForTests();

        bool first = PipeAvailability.IsListening(pipeName);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(first, PipeAvailability.IsListening(pipeName));
    }

    [Fact]
    public async Task ProbingDoesNotConsumeTheServersPendingConnection()
    {
        // The probe must observe the pipe without opening it. Testing the pipe path directly (for
        // example with File.Exists) connects, which completes this accept with a phantom client and
        // — while the server holds a single instance — locks the real run out entirely.
        using NamedPipeServerStream server = CreateServer();
        Task accept = server.WaitForConnectionAsync();

        PipeClient.ResetAvailabilityForTests();
        Assert.True(PipeAvailability.IsListening(pipeName));

        await Task.Delay(300);
        Assert.False(accept.IsCompleted);
        Assert.False(server.IsConnected);

        // A real client still gets through afterwards.
        using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000);
        await accept;
        Assert.True(server.IsConnected);
    }

    [Fact]
    public async Task ClientReportsUnavailableWithoutAListenerAndAvailableWithOne()
    {
        PipeClient.ResetAvailabilityForTests();
        Assert.True(PipeClient.IsKnownUnavailable(pipeName));

        using NamedPipeServerStream server = CreateServer();
        Task accept = server.WaitForConnectionAsync();
        PipeClient.ResetAvailabilityForTests();

        Assert.False(PipeClient.IsKnownUnavailable(pipeName));

        // Leave the accept loop in a defined state rather than abandoning it mid-test.
        using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000);
        await accept;
    }

    private NamedPipeServerStream CreateServer()
        => new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
}
