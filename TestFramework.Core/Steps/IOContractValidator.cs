using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Steps;

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
///   - A variable read through an immutable reference may not be written afterwards.
/// </summary>
internal static class IOContractValidator
{
    /// <param name="mainSteps">The linearly-ordered main stage steps.</param>
    /// <param name="externalVariables">Variables supplied to the run before any step executes.</param>
    /// <param name="externalArtifacts">Artifacts supplied to the run before any step executes.</param>
    /// <param name="variableTracker">
    /// The tracker built during preprocessing. Declared IO contracts do not carry immutability, so
    /// this is the only record of which reads demanded an immutable binding.
    /// </param>
    internal static void Validate(
        IReadOnlyList<StepGeneric> mainSteps,
        List<VariableIdentifier> externalVariables,
        List<ArtifactIdentifier> externalArtifacts,
        VariableTracker? variableTracker = null)
    {
        if (variableTracker is not null)
            ValidateImmutability(variableTracker);

        // key -> known producer metadata (external or last declared producer)
        var knownVars = new Dictionary<string, KnownContractValue>(StringComparer.OrdinalIgnoreCase);
        var knownArtifacts = new Dictionary<string, KnownContractValue>(StringComparer.OrdinalIgnoreCase);
        List<string> executedStepNames = [];

        foreach (var v in externalVariables)
            knownVars[v.Identifier] = KnownContractValue.External(v.Identifier);
        foreach (var a in externalArtifacts)
            knownArtifacts[a.Identifier] = KnownContractValue.External(a.Identifier);

        for (int stepIndex = 0; stepIndex < mainSteps.Count; stepIndex++)
        {
            StepGeneric step = mainSteps[stepIndex];
            var contract = step.IOContract;
            string stepName = step.LabelOptions.Label ?? step.Name;

            foreach (var input in contract.Inputs)
            {
                var lookup = input.Kind == StepIOKind.Variable ? knownVars : knownArtifacts;
                bool known = lookup.ContainsKey(input.Key);

                if (!known && input.Required)
                {
                    IReadOnlyList<string> availableKeys = [.. lookup.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)];
                    IReadOnlyList<string> similarKeys = FindSimilarKeys(input.Key, lookup.Keys);
                    throw new IOContractViolationException(
                        stepName,
                        input,
                        stepIndex,
                        [.. executedStepNames],
                        availableKeys,
                        similarKeys);
                }

                // Type compatibility - only when both sides declare a type
                if (known && input.DeclaredType != null && lookup[input.Key].DeclaredType is Type producerType)
                {
                    // The consumer expects input.DeclaredType; the producer emits producerType.
                    // The cast in VariableStore is (TConsumer?)object, so the producer value must
                    // be an instance of TConsumer — i.e. TConsumer must be assignable from producerType.
                    if (!input.DeclaredType.IsAssignableFrom(producerType))
                    {
                        KnownContractValue producer = lookup[input.Key];
                        throw new IOContractTypeViolationException(
                            stepName,
                            input,
                            producerType,
                            producer.SourceStepName,
                            producer.IsExternal);
                    }
                }
            }

            foreach (var output in contract.Outputs)
            {
                var lookup = output.Kind == StepIOKind.Variable ? knownVars : knownArtifacts;
                // Last declared producer wins (mirrors linear overwrite semantics)
                lookup[output.Key] = KnownContractValue.FromStep(output.Key, output.DeclaredType, stepName);
            }

            executedStepNames.Add(stepName);
        }
    }

    /// <summary>
    /// Fails when a variable that an earlier composition step read through an immutable reference is
    /// written afterwards. Reading a value immutably is a promise that it will not move underneath you.
    /// </summary>
    private static void ValidateImmutability(VariableTracker variableTracker)
    {
        HashSet<VariableIdentifier> readImmutably = [];

        foreach (VariableTracker.TrackedVariableOperation operation in variableTracker.GetRecordedOperations())
        {
            if (operation.IsWrite)
            {
                if (readImmutably.Contains(operation.Identifier))
                    throw new CannotSetImmutableVariableException(operation.Identifier);

                continue;
            }

            if (operation.RequiresImmutability)
                readImmutably.Add(operation.Identifier);
        }
    }

    private static IReadOnlyList<string> FindSimilarKeys(string missingKey, IEnumerable<string> availableKeys)
    {
        string normalizedMissingKey = NormalizeKey(missingKey);
        return [..
            availableKeys
                .Where(key => IsSimilarKey(normalizedMissingKey, NormalizeKey(key)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .Take(3)];
    }

    private static bool IsSimilarKey(string expected, string candidate)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(candidate))
            return false;

        return candidate.Contains(expected, StringComparison.OrdinalIgnoreCase)
            || expected.Contains(candidate, StringComparison.OrdinalIgnoreCase)
            || SharedPrefixLength(expected, candidate) >= 4;
    }

    private static int SharedPrefixLength(string left, string right)
    {
        int count = 0;
        int max = Math.Min(left.Length, right.Length);
        while (count < max && left[count] == right[count])
            count++;

        return count;
    }

    private static string NormalizeKey(string key)
    {
        char[] buffer = key.Where(char.IsLetterOrDigit).ToArray();
        return new string(buffer);
    }

    private sealed record KnownContractValue(string Key, Type? DeclaredType, string? SourceStepName, bool IsExternal)
    {
        public static KnownContractValue External(string key) => new(key, null, null, true);

        public static KnownContractValue FromStep(string key, Type? declaredType, string sourceStepName) => new(key, declaredType, sourceStepName, false);
    }
}
