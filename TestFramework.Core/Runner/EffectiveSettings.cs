using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Runner;

/// <summary>
/// One setting a run resolved for itself, and who resolved it.
/// </summary>
/// <param name="Source">Which package or component decided it, so two of them may both record a "Browser".</param>
/// <param name="Name">What was decided.</param>
/// <param name="Value">What it came out as.</param>
public readonly record struct EffectiveSetting(string Source, string Name, string Value)
{
    /// <summary>Reads as <c>ui.browser/Browser = chromium</c>.</summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString() => $"{this.Source}/{this.Name} = {this.Value}";
}

/// <summary>
/// What a run decided on your behalf, kept so the finished run can say so.
/// </summary>
/// <remarks>
/// <para>
/// §5 lets a value default when it is operational rather than meaning-changing, on three conditions, and the
/// third is that the effective value is readable from the frozen run - "the record must never depend on
/// remembering which framework version ran it". A run that found a browser rather than being told one, or
/// started a container from an image pinned in code, could say what it *did* and not what it did it *with*.
/// This is where it says.
/// </para>
/// <para>
/// <strong>Not a channel, and that distinction is why it is its own thing.</strong> Variables, artifacts,
/// the per-package live slot and the run's resource values are all read back during the run to decide
/// something. Nothing reads this to decide anything - it is written for whoever reads the run afterwards.
/// Putting it in the resource values instead would have meant inventing a resource for a browser to hang
/// off, and a browser is not a resource: it has no coordinates and nothing connects to it.
/// </para>
/// <para>
/// It freezes with the run like everything else under §2, and deliberately not through the public
/// <c>IFreezable</c>: a caller able to close a *running* run's record could stop a component recording what
/// it had just chosen. Reading stays open forever, which is the entire point of keeping it.
/// </para>
/// </remarks>
public sealed class EffectiveSettings
{
    private readonly object syncRoot = new object();
    private readonly Dictionary<(string Source, string Name), EffectiveSetting> settings = [];
    private bool frozen;

    /// <summary>
    /// Records what this run resolved a setting to.
    /// </summary>
    /// <remarks>
    /// Recording the same thing twice is fine and recording it differently is not: one run used one browser,
    /// and two answers means something is deciding it twice. That is a mistake worth hearing about rather
    /// than a last-writer-wins race, which is the same rule the resource values keep for two producers.
    /// </remarks>
    /// <param name="source">Which package or component decided it.</param>
    /// <param name="name">What was decided.</param>
    /// <param name="value">What it came out as.</param>
    /// <exception cref="FrameworkStateException">The run has finished.</exception>
    /// <exception cref="FrameworkConfigurationException">The same setting was already recorded differently.</exception>
    public void Record(string source, string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        lock (this.syncRoot)
        {
            if (this.frozen)
            {
                throw new FrameworkStateException(
                    $"This run has finished, so '{source}/{name}' cannot be recorded on it any more.");
            }

            if (this.settings.TryGetValue((source, name), out EffectiveSetting existing)
                && !string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                throw new FrameworkConfigurationException(
                    $"'{source}/{name}' was already recorded as '{existing.Value}' for this run, and is now being recorded as '{value}'.",
                    ["Record it once, where the choice is actually made."]);
            }

            this.settings[(source, name)] = new EffectiveSetting(source, name, value);
        }
    }

    /// <summary>
    /// Everything this run decided, ordered so two runs of one test read the same.
    /// </summary>
    /// <returns>The settings.</returns>
    public IReadOnlyList<EffectiveSetting> Snapshot()
    {
        lock (this.syncRoot)
        {
            return [.. this.settings.Values
                .OrderBy(static setting => setting.Source, StringComparer.Ordinal)
                .ThenBy(static setting => setting.Name, StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// What one source recorded, when it recorded it.
    /// </summary>
    /// <param name="source">Which package or component.</param>
    /// <param name="name">Which setting.</param>
    /// <param name="value">The value it resolved to.</param>
    /// <returns>True when this run recorded it.</returns>
    public bool TryGet(string source, string name, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (this.syncRoot)
        {
            bool found = this.settings.TryGetValue((source, name), out EffectiveSetting setting);
            value = found ? setting.Value : null;

            return found;
        }
    }

    /// <summary>
    /// Closes the record when the run ends.
    /// </summary>
    internal void FreezeForRunEnd()
    {
        lock (this.syncRoot)
        {
            this.frozen = true;
        }
    }
}
