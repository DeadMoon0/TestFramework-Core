using System;
using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Describes the declared input and output dependencies of a step.
/// </summary>
/// <remarks>
/// A step declares what it reads and writes with the methods here. Each takes the identifier type it is about,
/// so the kind of an entry follows from what is passed rather than from a second argument that could disagree
/// with it, and a reference that holds a constant declares nothing - there is no variable to depend on. That
/// last rule used to be a guard every step wrote by hand before reaching for <c>Identifier!</c>.
/// </remarks>
public class StepIOContract : IFreezable
{
    private readonly FreezableCollection<StepIOEntry> inputs = new();
    private readonly FreezableCollection<StepIOEntry> outputs = new();

    /// <summary>
    /// Gets a value indicating whether the contract has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the contract and its declared input and output collections.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        this.inputs.Freeze();
        this.outputs.Freeze();
    }

    /// <summary>
    /// Gets the declared input dependencies.
    /// </summary>
    public IReadOnlyCollection<StepIOEntry> Inputs => this.inputs;

    /// <summary>
    /// Gets the declared output dependencies.
    /// </summary>
    public IReadOnlyCollection<StepIOEntry> Outputs => this.outputs;

    /// <summary>
    /// Gets a value indicating whether the contract declares any inputs or outputs.
    /// </summary>
    public bool HasDeclarations => this.inputs.Count > 0 || this.outputs.Count > 0;

    /// <summary>
    /// Declares that the step needs the variable <paramref name="reference"/> names, typed as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type the step reads it as.</typeparam>
    /// <param name="reference">The reference; one holding a constant, or none, declares nothing.</param>
    /// <returns>This contract.</returns>
    public StepIOContract Input<T>(VariableReference<T>? reference)
        => this.AddReference(reference, required: true, typeof(T));

    /// <summary>
    /// Declares that the step reads the variable <paramref name="reference"/> names when it exists, typed as <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type the step reads it as.</typeparam>
    /// <param name="reference">The reference; one holding a constant, or none, declares nothing.</param>
    /// <returns>This contract.</returns>
    public StepIOContract OptionalInput<T>(VariableReference<T>? reference)
        => this.AddReference(reference, required: false, typeof(T));

    /// <summary>
    /// Declares that the step needs the variable <paramref name="reference"/> names, without saying its type.
    /// </summary>
    /// <param name="reference">The reference; one holding a constant, or none, declares nothing.</param>
    /// <returns>This contract.</returns>
    public StepIOContract Input(VariableReferenceGeneric? reference)
        => this.AddReference(reference, required: true, null);

    /// <summary>
    /// Declares that the step reads the variable <paramref name="reference"/> names when it exists, without saying its type.
    /// </summary>
    /// <param name="reference">The reference; one holding a constant, or none, declares nothing.</param>
    /// <returns>This contract.</returns>
    public StepIOContract OptionalInput(VariableReferenceGeneric? reference)
        => this.AddReference(reference, required: false, null);

    /// <summary>
    /// Declares that the step needs a variable, without saying its type.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <returns>This contract.</returns>
    public StepIOContract Input(VariableIdentifier identifier)
        => this.Add(this.inputs, Variable(identifier), StepIOKind.Variable, true, null);

    /// <summary>
    /// Declares that the step needs a variable of a type known only at run time.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <param name="declaredType">The type the step reads it as.</param>
    /// <returns>This contract.</returns>
    public StepIOContract Input(VariableIdentifier identifier, Type declaredType)
        => this.Add(this.inputs, Variable(identifier), StepIOKind.Variable, true, Type(declaredType));

    /// <summary>
    /// Declares that the step reads a variable when it exists, without saying its type.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <returns>This contract.</returns>
    public StepIOContract OptionalInput(VariableIdentifier identifier)
        => this.Add(this.inputs, Variable(identifier), StepIOKind.Variable, false, null);

    /// <summary>
    /// Declares that the step reads a variable of a type known only at run time, when it exists.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <param name="declaredType">The type the step reads it as.</param>
    /// <returns>This contract.</returns>
    public StepIOContract OptionalInput(VariableIdentifier identifier, Type declaredType)
        => this.Add(this.inputs, Variable(identifier), StepIOKind.Variable, false, Type(declaredType));

    /// <summary>
    /// Declares that the step writes a variable, without saying its type.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <returns>This contract.</returns>
    public StepIOContract Output(VariableIdentifier identifier)
        => this.Add(this.outputs, Variable(identifier), StepIOKind.Variable, true, null);

    /// <summary>
    /// Declares that the step writes a variable of the given type.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <param name="declaredType">The type it writes.</param>
    /// <returns>This contract.</returns>
    public StepIOContract Output(VariableIdentifier identifier, Type declaredType)
        => this.Add(this.outputs, Variable(identifier), StepIOKind.Variable, true, Type(declaredType));

    /// <summary>
    /// Declares that the step may write a variable, without saying its type.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <returns>This contract.</returns>
    public StepIOContract OptionalOutput(VariableIdentifier identifier)
        => this.Add(this.outputs, Variable(identifier), StepIOKind.Variable, false, null);

    /// <summary>
    /// Declares that the step may write a variable of the given type.
    /// </summary>
    /// <param name="identifier">The variable.</param>
    /// <param name="declaredType">The type it writes.</param>
    /// <returns>This contract.</returns>
    public StepIOContract OptionalOutput(VariableIdentifier identifier, Type declaredType)
        => this.Add(this.outputs, Variable(identifier), StepIOKind.Variable, false, Type(declaredType));

    /// <summary>
    /// Declares that the step needs an artifact.
    /// </summary>
    /// <param name="identifier">The artifact.</param>
    /// <returns>This contract.</returns>
    public StepIOContract InputArtifact(ArtifactIdentifier identifier)
        => this.Add(this.inputs, Artifact(identifier), StepIOKind.Artifact, true, null);

    /// <summary>
    /// Declares that the step produces or changes an artifact.
    /// </summary>
    /// <param name="identifier">The artifact.</param>
    /// <returns>This contract.</returns>
    public StepIOContract OutputArtifact(ArtifactIdentifier identifier)
        => this.Add(this.outputs, Artifact(identifier), StepIOKind.Artifact, true, null);

    /// <summary>
    /// Copies the current contract entries to another instance.
    /// </summary>
    /// <param name="target">The target contract instance.</param>
    public void CloneTo(StepIOContract target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (StepIOEntry input in this.inputs) target.inputs.Add(input);
        foreach (StepIOEntry output in this.outputs) target.outputs.Add(output);
    }

    private StepIOContract AddReference(VariableReferenceGeneric? reference, bool required, Type? declaredType)
        => reference?.Identifier is { } identifier
            ? this.Add(this.inputs, identifier.Identifier, StepIOKind.Variable, required, declaredType)
            : this;

    private StepIOContract Add(FreezableCollection<StepIOEntry> entries, string key, StepIOKind kind, bool required, Type? declaredType)
    {
        entries.Add(new StepIOEntry(key, kind, required, declaredType));
        return this;
    }

    private static string Variable(VariableIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return identifier.Identifier;
    }

    private static string Artifact(ArtifactIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return identifier.Identifier;
    }

    private static Type Type(Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        return declaredType;
    }
}
