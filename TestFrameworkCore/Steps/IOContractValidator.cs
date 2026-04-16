using System;
using System.Collections.Generic;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Exceptions;
using TestFrameworkCore.Steps.Options;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Steps;

/// <summary>
/// Validates declared IO contracts across the linearly-ordered main stage steps.
/// Replaces the VariableTracker / ArtifactTracker EnsureValidity checks.
///
/// Rules:
///   - Every required input must be produced by a prior step's declared output
///     or by an externally supplied variable/artifact.
///   - When both producer-output and consumer-input carry a DeclaredType, the
///     producer type must be assignable TO the consumer type
///     (mirrors VariableStore.GetVariable&lt;T&gt;() cast semantics).
///   - Steps with no IOContract declarations are transparent to this validator.
/// </summary>
internal static class IOContractValidator
{
    internal static void Validate(
        IReadOnlyList<StepGeneric> mainSteps,
        List<VariableIdentifier> externalVariables,
        List<ArtifactIdentifier> externalArtifacts)
    {
        // key → declared producer type (null = known but no type declared)
        var knownVars = new Dictionary<string, Type?>(StringComparer.OrdinalIgnoreCase);
        var knownArtifacts = new Dictionary<string, Type?>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in externalVariables) knownVars[v.Identifier] = null;
        foreach (var a in externalArtifacts) knownArtifacts[a.Identifier] = null;

        foreach (var step in mainSteps)
        {
            var contract = step.IOContract;
            string stepName = step.LabelOptions.Label ?? step.Name;

            foreach (var input in contract.Inputs)
            {
                var lookup = input.Kind == StepIOKind.Variable ? knownVars : knownArtifacts;
                bool known = lookup.ContainsKey(input.Key);

                if (!known && input.Required)
                    throw new IOContractViolationException(stepName, input);

                // Type compatibility - only when both sides declare a type
                if (known && input.DeclaredType != null && lookup[input.Key] is Type producerType)
                {
                    // The consumer expects input.DeclaredType; the producer emits producerType.
                    // The cast in VariableStore is (TConsumer?)object, so the producer value must
                    // be an instance of TConsumer — i.e. TConsumer must be assignable from producerType.
                    if (!input.DeclaredType.IsAssignableFrom(producerType))
                        throw new IOContractTypeViolationException(stepName, input, producerType);
                }
            }

            foreach (var output in contract.Outputs)
            {
                var lookup = output.Kind == StepIOKind.Variable ? knownVars : knownArtifacts;
                // Last declared producer wins (mirrors linear overwrite semantics)
                lookup[output.Key] = output.DeclaredType;
            }
        }
    }
}
