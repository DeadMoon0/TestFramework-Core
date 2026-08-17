using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Tests;

/// <summary>
/// Covers the request/reply every step makes before it runs.
/// </summary>
/// <remarks>
/// A step that is not meant to pause still asks whether it should, so this exchange sits in front of
/// every step of every attached run. When it went wrong it did not fail — it waited, which is why a
/// sample that ran in six seconds took half an hour with the UI attached and read as a hang.
/// </remarks>
public sealed class PipeBreakpointExchangeTests
{
    /// <summary>
    /// How long an unanswered wait lasts here.
    /// </summary>
    /// <remarks>
    /// The product default is ten minutes, which is the right answer for a developer who really is
    /// parked on a breakpoint and the wrong one for a test. Shortened so a regression costs seconds
    /// and is still unmistakable: a dropped answer takes at least this long, and a delivered one
    /// takes a fraction of a millisecond, so there is nothing in between to be borderline about.
    /// </remarks>
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many steps to run through the exchange.
    /// </summary>
    /// <remarks>
    /// The fault was a race, so one exchange proves nothing: the reply has to arrive before the
    /// sender registers its waiter, and sometimes it did not. In the run that exposed this, roughly
    /// two steps in five lost. A dozen makes surviving the race by luck a one-in-five-hundred event,
    /// while bounding the worst case to about twenty-five seconds.
    /// </remarks>
    private const int Steps = 12;

    /// <summary>
    /// The rule that makes the fault impossible, asserted on the shape rather than on the clock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fault was that a reply could arrive before the caller had registered anything to receive
    /// it, and an unmatched reply is dropped rather than queued. What fixed it was not a faster path
    /// but the removal of the two-call shape: there is no longer any way to wait for a reply except
    /// by sending the request in the same call, so the registration cannot be late.
    /// </para>
    /// <para>
    /// This is asserted structurally because the race cannot be reproduced by timing it — see the
    /// test below. Reintroducing a bare wait-for-a-reply method is the way the fault comes back, and
    /// this is what notices.
    /// </para>
    /// </remarks>
    [Fact]
    public void AReplyCanOnlyBeAwaitedByTheCallThatSendsTheRequest()
    {
        System.Reflection.MethodInfo[] awaitingAReply =
        [
            .. typeof(PipeClient)
                .GetMethods(System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(method => method.ReturnType == typeof(Task<IPipeSignal>))
        ];

        Assert.NotEmpty(awaitingAReply);

        Assert.All(
            awaitingAReply,
            method => Assert.True(
                method.GetParameters().Any(parameter => typeof(IPipeSignal).IsAssignableFrom(parameter.ParameterType)),
                $"'{method.Name}' waits for a reply without sending the request, so its waiter is registered after something else sent one. That is the race this transport had: an answer arriving in between is dropped and the caller waits out the whole timeout."));
    }

    /// <summary>
    /// A step is released promptly rather than waiting out the timeout.
    /// </summary>
    /// <remarks>
    /// <b>This does not reproduce the original race.</b> Reproducing it needs the reply to beat the
    /// sender's own continuation, and with the consumer in this process it never does — the fault was
    /// found against a separate, already-running UI process. Run against the broken ordering this
    /// passes every time, so it must not be mistaken for the guard; the structural test above is
    /// that. What this covers is the symptom whatever the cause: if the exchange ever starts hitting
    /// its timeout — a wrong default, a consumer that stops answering, a framing fault — every step
    /// of every attached run pays for it, and that is worth catching however it arises.
    /// </remarks>
    [Fact]
    public async Task AStepIsReleasedWhenTheAnswerArrivesRatherThanWaitingOutTheTimeout()
    {
        string pipeName = "tf-breakpoint-" + Guid.NewGuid().ToString("N");

        using PipeScope scope = PipeScope.PointedAt(pipeName, WaitTimeout);
        using CancellationTokenSource life = new(TimeSpan.FromMinutes(2));

        await using DebuggerThatAlwaysContinues consumer = DebuggerThatAlwaysContinues.Listening(pipeName, life.Token);
        using PipeRunDebugger debugger = new();

        TimeSpan slowest = TimeSpan.Zero;
        int slowestStep = -1;

        for (int step = 0; step < Steps; step++)
        {
            Stopwatch exchange = Stopwatch.StartNew();
            await debugger.SignalAndWaitBreakpointHitAsync("session-1", "Main", step);
            exchange.Stop();

            if (exchange.Elapsed > slowest)
            {
                slowest = exchange.Elapsed;
                slowestStep = step;
            }
        }

        // Proves the exchange really happened over the wire. Without it a transport that had quietly
        // turned itself off would return instantly and pass everything below.
        Assert.Equal(Steps, consumer.RequestsSeen);

        Assert.True(
            slowest < WaitTimeout,
            $"Step {slowestStep} took {slowest.TotalMilliseconds:N0} ms. An answer arrived and was dropped, so the step waited out the {WaitTimeout.TotalMilliseconds:N0} ms timeout instead of being released.");
    }

    /// <summary>
    /// Points the transport at a pipe of this test's own, with a timeout it can afford to hit.
    /// </summary>
    /// <remarks>
    /// A name per test rather than the well-known one, so a developer with the real UI open does not
    /// have their debugger answer these requests — and so this test cannot answer their run's.
    /// </remarks>
    private sealed class PipeScope : IDisposable
    {
        private const string NameVariable = "TESTFRAMEWORK_DEBUG_PIPE_NAME";
        private const string ModeVariable = "TESTFRAMEWORK_DEBUG_PIPE";
        private const string WaitVariable = "TESTFRAMEWORK_DEBUG_PIPE_WAIT_MS";

        private readonly string? previousName;
        private readonly string? previousMode;
        private readonly string? previousWait;

        private PipeScope(string? previousName, string? previousMode, string? previousWait)
        {
            this.previousName = previousName;
            this.previousMode = previousMode;
            this.previousWait = previousWait;
        }

        internal static PipeScope PointedAt(string pipeName, TimeSpan waitTimeout)
        {
            PipeScope scope = new(
                System.Environment.GetEnvironmentVariable(NameVariable),
                System.Environment.GetEnvironmentVariable(ModeVariable),
                System.Environment.GetEnvironmentVariable(WaitVariable));

            System.Environment.SetEnvironmentVariable(NameVariable, pipeName);
            System.Environment.SetEnvironmentVariable(ModeVariable, "on");
            System.Environment.SetEnvironmentVariable(WaitVariable, ((int)waitTimeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture));

            return scope;
        }

        public void Dispose()
        {
            System.Environment.SetEnvironmentVariable(NameVariable, previousName);
            System.Environment.SetEnvironmentVariable(ModeVariable, previousMode);
            System.Environment.SetEnvironmentVariable(WaitVariable, previousWait);
        }
    }

    /// <summary>
    /// A consumer that answers every breakpoint request the moment it reads one.
    /// </summary>
    /// <remarks>
    /// Answering as fast as possible is the point rather than an incidental detail: the fault only
    /// appeared when the reply beat the sender to registering its waiter, so a consumer that paused
    /// to think would hide exactly what this test is here to catch.
    /// </remarks>
    private sealed class DebuggerThatAlwaysContinues : IAsyncDisposable
    {
        private readonly NamedPipeServerStream server;
        private readonly CancellationTokenSource life;
        private readonly Task pump;

        private int requestsSeen;

        private DebuggerThatAlwaysContinues(NamedPipeServerStream server, CancellationTokenSource life)
        {
            this.server = server;
            this.life = life;

            pump = PumpAsync(life.Token);
        }

        /// <summary>How many requests reached this consumer.</summary>
        internal int RequestsSeen => Volatile.Read(ref requestsSeen);

        internal static DebuggerThatAlwaysContinues Listening(string pipeName, CancellationToken cancellationToken)
        {
            // CurrentUserOnly to match the real consumer. The client sets it too and would connect
            // to this either way while both run as one user, so leaving it off would quietly make
            // the test the only pipe here not carrying the owner-only ACL the transport relies on.
            NamedPipeServerStream server = new(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            return new DebuggerThatAlwaysContinues(server, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        }

        public async ValueTask DisposeAsync()
        {
            await life.CancelAsync();

            try
            {
                await pump;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            life.Dispose();
            await server.DisposeAsync();
        }

        private async Task PumpAsync(CancellationToken cancellationToken)
        {
            try
            {
                await server.WaitForConnectionAsync(cancellationToken);

                PipeProtocolStream framing = new(server);

                while (!cancellationToken.IsCancellationRequested)
                {
                    IPipeSignal? signal = await framing.WaitSignalAsync(cancellationToken);

                    if (signal is null)
                        break;

                    if (signal is not PipeBreakpointHitRequestSignal request)
                        continue;

                    // Counted before the answer goes out. Counting after would be a race of this
                    // test's own making: the step can be released and the assertion read before the
                    // increment lands.
                    Interlocked.Increment(ref requestsSeen);

                    await framing.SendSignalAsync(new PipeBreakpointHitContinueSignal { SessionId = request.SessionId });
                }
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
