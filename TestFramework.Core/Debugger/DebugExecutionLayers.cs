using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Derives the execution layers the console output draws a stage in, from the stage's step declarations.
/// </summary>
internal static class DebugExecutionLayers
{
    internal sealed record ExecutionLayerPlan(int LayerIndex, int[] StepIds, StepExecutionPhase Phase);

    internal static IReadOnlyList<ExecutionLayerPlan> Build(DebugStepState[] steps)
    {
        Dictionary<int, List<int>> dependents = Enumerable.Range(0, steps.Length).ToDictionary(index => index, _ => new List<int>());
        Dictionary<int, int> indegree = Enumerable.Range(0, steps.Length).ToDictionary(index => index, _ => 0);

        for (int leftIndex = 0; leftIndex < steps.Length; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < steps.Length; rightIndex++)
            {
                if (!RequiresSequentialOrdering(steps[leftIndex], steps[rightIndex]))
                    continue;

                dependents[leftIndex].Add(rightIndex);
                indegree[rightIndex]++;
            }
        }

        List<ExecutionLayerPlan> layers = [];
        HashSet<int> scheduled = [];

        while (scheduled.Count < steps.Length)
        {
            int[] readyStepIds = indegree
                .Where(entry => !scheduled.Contains(entry.Key) && entry.Value == 0)
                .Select(entry => entry.Key)
                .OrderBy(index => index)
                .ToArray();

            if (readyStepIds.Length == 0)
                throw new TestFramework.Core.Exceptions.DependencyGraphException("Unable to derive debugger-visible execution layers for the stage.");

            int layerIndex = layers.Count;
            layers.Add(new ExecutionLayerPlan(layerIndex, readyStepIds, steps[readyStepIds[0]].Phase));

            foreach (int readyStepId in readyStepIds)
            {
                scheduled.Add(readyStepId);
                foreach (int dependent in dependents[readyStepId])
                    indegree[dependent]--;
            }
        }

        return layers;
    }

    private static bool RequiresSequentialOrdering(DebugStepState left, DebugStepState right)
    {
        if (RequiresPhaseOrdering(left, right))
            return true;

        if (left.Parallelization == StepParallelizationMode.DoNotParallelize || right.Parallelization == StepParallelizationMode.DoNotParallelize)
            return true;

        return HasAccessConflict(left, right);
    }

    private static bool RequiresPhaseOrdering(DebugStepState left, DebugStepState right)
    {
        if (left.Phase != right.Phase)
            return true;

        return !IsMergeablePhase(left.Phase);
    }

    private static bool IsMergeablePhase(StepExecutionPhase phase)
    {
        return phase is StepExecutionPhase.Prepare or StepExecutionPhase.Materialize;
    }

    private static bool HasAccessConflict(DebugStepState left, DebugStepState right)
    {
        foreach (DebugStepIo leftOutput in left.Outputs)
        {
            if (ContainsEntry(right.Inputs, leftOutput) || ContainsEntry(right.Outputs, leftOutput))
                return true;
        }

        foreach (DebugStepIo leftInput in left.Inputs)
        {
            if (ContainsEntry(right.Outputs, leftInput))
                return true;
        }

        return false;
    }

    private static bool ContainsEntry(IEnumerable<DebugStepIo> entries, DebugStepIo candidate)
    {
        return entries.Any(entry => entry.Kind == candidate.Kind && StringComparer.Ordinal.Equals(entry.Key, candidate.Key));
    }
}
