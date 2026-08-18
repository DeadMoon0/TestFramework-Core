using System.ComponentModel;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Debugger;

/// <summary>
/// A step as it was declared: what it is called, when it runs, what it reads and writes, and the policies it
/// runs under.
/// </summary>
/// <remarks>
/// <para>
/// Facts, resolved. This record used to carry the builder objects themselves — <c>RetryOptions</c>,
/// <c>TimeOutOptions</c>, <c>ExecutionOptions</c> and three more — which meant every step on the wire shipped
/// five <c>IsFrozen</c> flags, three <c>RequireImmutability</c> flags and a derived <c>HasDeclarations</c>,
/// while the numbers a reader actually wants were nowhere: a retry count and a timeout live behind a method on
/// a variable reference, so they serialized as an empty wrapper. Nobody could learn from a journal that a step
/// retries three times.
/// </para>
/// <para>
/// A policy a test pinned to a variable is stated as that variable's name rather than a value, because at the
/// moment the plan is sent the variable may not have been written yet. Naming it says the same thing honestly:
/// this is decided later, and here is where it comes from.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public record DebugStepState
{
    /// <summary>Gets the step name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the step description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the label a test gave this step, when it gave one.</summary>
    public string? Label { get; init; }

    /// <summary>Gets which phase of the stage the step belongs to.</summary>
    public required StepExecutionPhase Phase { get; init; }

    /// <summary>Gets whether the step produces a result.</summary>
    public required bool DoesReturn { get; init; }

    /// <summary>
    /// Gets which layer of the stage the step runs in.
    /// </summary>
    /// <remarks>
    /// Steps sharing a layer can run together. Resolved by the same planner the runner uses, so the layers
    /// reported are the layers that run.
    /// </remarks>
    public int LayerIndex { get; init; }

    /// <summary>Gets whether the step may run beside its neighbours.</summary>
    public required StepParallelizationMode Parallelization { get; init; }

    /// <summary>Gets how many times the step may be retried, when that was fixed when the run was planned.</summary>
    public int? MaxRetries { get; init; }

    /// <summary>Gets the variable the retry count is read from, when a test pinned it to one.</summary>
    public string? MaxRetriesVariable { get; init; }

    /// <summary>Gets how long the step may run for, when that was fixed when the run was planned.</summary>
    public System.TimeSpan? TimeOut { get; init; }

    /// <summary>Gets the variable the timeout is read from, when a test pinned it to one.</summary>
    public string? TimeOutVariable { get; init; }

    /// <summary>
    /// Gets the exception types this step is allowed to throw without failing the run.
    /// </summary>
    /// <remarks>
    /// Type names, not CLR types. The wire carried <see cref="System.Type"/> objects before, which a consumer
    /// in another process cannot load and has no use for beyond the name.
    /// </remarks>
    public string[] IgnoredExceptions { get; init; } = [];

    /// <summary>Gets the values the step declares it reads.</summary>
    public DebugStepIo[] Inputs { get; init; } = [];

    /// <summary>Gets the values the step declares it writes.</summary>
    public DebugStepIo[] Outputs { get; init; } = [];
}

/// <summary>One value a step declares it reads or writes.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record DebugStepIo
{
    /// <summary>Gets the variable or artifact identifier.</summary>
    public required string Key { get; init; }

    /// <summary>Gets whether it is a variable or an artifact.</summary>
    public required StepIOKind Kind { get; init; }

    /// <summary>Gets whether the step cannot run without it.</summary>
    public bool Required { get; init; } = true;

    /// <summary>Gets the type the step declared, by name, when it declared one.</summary>
    public string? DeclaredType { get; init; }
}
