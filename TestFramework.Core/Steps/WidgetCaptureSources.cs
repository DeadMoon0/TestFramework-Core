using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Logging;

namespace TestFramework.Core.Steps;

/// <summary>
/// The sources a run can be asked for fresh evidence, and the guarantee that asking cannot cost the run.
/// </summary>
/// <remarks>
/// <para>
/// Collected once per run from what the caller registered, the same way <see cref="StepObservers"/> is
/// and for the same reasons: a source registered as transient would otherwise be a different instance
/// every time it was asked, and a container answering both the single and the collection question
/// would have its source asked twice for one request.
/// </para>
/// <para>
/// The fan-out and the isolation live here rather than in the transport, so the rule — a capture never
/// changes what the run is doing — is stated in one place, next to the other one that says it.
/// </para>
/// </remarks>
internal sealed class WidgetCaptureSources
{
    /// <summary>No sources at all — what a run gets when nobody registered one.</summary>
    internal static WidgetCaptureSources None { get; } = new WidgetCaptureSources([]);

    private readonly IReadOnlyList<IWidgetCaptureSource> sources;

    private WidgetCaptureSources(IReadOnlyList<IWidgetCaptureSource> sources)
    {
        this.sources = sources;
    }

    /// <summary>Gets a value indicating whether there is anything here to ask.</summary>
    internal bool IsEmpty => this.sources.Count == 0;

    /// <summary>
    /// Collects the capture sources a caller registered.
    /// </summary>
    /// <param name="serviceProvider">The run's services, or null when there are none.</param>
    /// <returns>The sources, or <see cref="None"/> when nothing is registered.</returns>
    internal static WidgetCaptureSources For(IServiceProvider? serviceProvider)
    {
        if (serviceProvider is null)
        {
            return None;
        }

        List<IWidgetCaptureSource> found = [];

        Type collectionType = typeof(IEnumerable<>).MakeGenericType(typeof(IWidgetCaptureSource));

        if (serviceProvider.GetService(collectionType) is IEnumerable registered)
        {
            foreach (object? candidate in registered)
            {
                Add(found, candidate as IWidgetCaptureSource);
            }
        }

        Add(found, serviceProvider.GetService(typeof(IWidgetCaptureSource)) as IWidgetCaptureSource);

        return found.Count == 0 ? None : new WidgetCaptureSources([.. found]);
    }

    /// <summary>
    /// Asks every source for what it has, in turn.
    /// </summary>
    /// <remarks>
    /// One at a time rather than together. Two sources photographing two browsers both want the page
    /// to hold still, and a run stopped at a breakpoint has all the time in the world — parallelism
    /// here would buy a fraction of a second and pay for it in pictures of pages mid-navigation.
    /// </remarks>
    /// <param name="run">Builds the context to hand over, called once per source so two cannot spend
    /// each other's budget.</param>
    /// <param name="logger">The run's logger, for anything a source throws.</param>
    /// <returns>What went wrong, or null when every source finished.</returns>
    internal async Task<string?> CaptureAsync(Func<RunContext> run, ScopedLogger logger)
    {
        string? problem = null;

        foreach (IWidgetCaptureSource source in this.sources)
        {
            try
            {
                await source.CaptureAsync(run()).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Swallowed for the reason an observer's exceptions are: a source is asked what it can
                // show, and it does not get a say in the run. Reported back to whoever asked, though —
                // a person pressing a button is owed the reason it did nothing.
                logger.LogWarning(
                    "{0} threw while capturing evidence on request. The run is unaffected.\n{1}",
                    source.GetType().Name,
                    exception.ToString());

                problem ??= $"{source.GetType().Name} could not capture: {exception.Message}";
            }
        }

        return problem;
    }

    private static void Add(List<IWidgetCaptureSource> sources, IWidgetCaptureSource? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        // A container that answers both the single and the collection question hands back the same
        // instance twice; asking it twice would double every picture the request produced.
        foreach (IWidgetCaptureSource known in sources)
        {
            if (ReferenceEquals(known, candidate))
            {
                return;
            }
        }

        sources.Add(candidate);
    }
}
