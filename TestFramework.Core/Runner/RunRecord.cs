using System;
using System.Collections.Generic;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Runner;

/// <summary>
/// A package's own append-only record of what happened during one run, kept under the run's rules.
/// </summary>
/// <remarks>
/// <para>
/// Some packages record things the run's channels do not carry: a mock writes down every call the system under
/// test made to it. Each used to keep its own "has the run finished?" flag, flipped by its own teardown - a copy of
/// the rule every Core channel follows, and one that drifted: it closed when the package's teardown ran rather than
/// when the run ended, so a run whose teardown never ran left the record open for good.
/// </para>
/// <para>
/// A record follows the run's one write gate instead, and it writes as the run itself: it finishes when the run
/// finishes, and nothing earlier closes it. Not as the step that opened it - a record outlives that step by design.
/// An environment component opens one while the run's setup step runs, and the entries arrive afterwards, from code
/// no step owns; written as that step's attempt, every one of them would be refused the moment the step ended.
/// </para>
/// <para>
/// A refused entry is dropped and reported, never thrown: a record is written by code the run does not control,
/// often on that code's own threads, and an exception there could take the test process down for the sake of an
/// entry nobody can assert on any more.
/// </para>
/// </remarks>
/// <typeparam name="T">What one entry is.</typeparam>
public sealed class RunRecord<T>
{
    private readonly object syncRoot = new object();
    private readonly List<T> entries = [];
    private readonly RunWriter writer;
    private readonly ScopedLogger logger;
    private readonly RunChannel channel;

    private RunRecord(string name, RunWriter writer, ScopedLogger logger)
    {
        this.Name = name;
        this.writer = writer;
        this.logger = logger;
        this.channel = RunChannel.Record(name);
    }

    /// <summary>
    /// The record's name, used in messages.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// How many entries the record holds.
    /// </summary>
    public int Count
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.entries.Count;
            }
        }
    }

    /// <summary>
    /// Opens a record that lives and finishes with <paramref name="context"/>'s run.
    /// </summary>
    /// <param name="context">The context of the step or environment component that owns the record.</param>
    /// <param name="name">What the record holds, for messages - for example <c>mock calls</c>.</param>
    /// <returns>The record.</returns>
    public static RunRecord<T> Open(RunContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new RunRecord<T>(name, context.Variables.Writer.Gate.Unrestricted, context.Logger);
    }

    /// <summary>
    /// Adds an entry, unless the run has finished.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>True when the entry was kept; false when it was dropped, which is reported in the run's log.</returns>
    public bool Append(T entry)
    {
        if (!this.writer.TryAdmit(this.channel, $"#{this.Count + 1}", this.logger, out RunWriteTicket? ticket))
            return false;

        using (ticket)
        lock (this.syncRoot)
        {
            this.entries.Add(entry);
            return true;
        }
    }

    /// <summary>
    /// The entries so far, in the order they were added, as a copy nothing else can change.
    /// </summary>
    /// <returns>The entries.</returns>
    public IReadOnlyList<T> Snapshot()
    {
        lock (this.syncRoot)
        {
            return Array.AsReadOnly(this.entries.ToArray());
        }
    }
}
