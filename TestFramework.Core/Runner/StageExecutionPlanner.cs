using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Steps.SystemSteps;

namespace TestFramework.Core.Runner;

internal sealed class StageExecutionPlanner
{
    private readonly StageInstance stageInstance;
    private readonly ArtifactStore artifactStore;
    private readonly ScheduledStep[] scheduledSteps;

    internal StageExecutionPlanner(StageInstance stageInstance, ArtifactStore artifactStore)
    {
        this.stageInstance = stageInstance;
        this.artifactStore = artifactStore;
        scheduledSteps = stageInstance.Steps.Select((step, index) => new ScheduledStep(index, step)).ToArray();
    }

    internal IReadOnlyList<IReadOnlyList<ScheduledStep>> BuildLayers()
    {
        Dictionary<int, List<int>> dependents = scheduledSteps.ToDictionary(x => x.Index, _ => new List<int>());
        Dictionary<int, int> indegree = scheduledSteps.ToDictionary(x => x.Index, _ => 0);

        for (int leftIndex = 0; leftIndex < scheduledSteps.Length; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < scheduledSteps.Length; rightIndex++)
            {
                if (!RequiresSequentialOrdering(scheduledSteps[leftIndex].Step.Step, scheduledSteps[rightIndex].Step.Step))
                    continue;

                dependents[leftIndex].Add(rightIndex);
                indegree[rightIndex]++;
            }
        }

        List<IReadOnlyList<ScheduledStep>> layers = [];
        HashSet<int> scheduled = [];

        while (scheduled.Count < scheduledSteps.Length)
        {
            ScheduledStep[] readySteps = scheduledSteps
                .Where(x => !scheduled.Contains(x.Index) && indegree[x.Index] == 0)
                .ToArray();

            if (readySteps.Length == 0)
                throw new InvalidOperationException($"Could not build an execution plan for stage '{stageInstance.Stage.Name}'. The step dependency graph contains a cycle.");

            layers.Add(readySteps);

            foreach (ScheduledStep readyStep in readySteps)
            {
                scheduled.Add(readyStep.Index);
                foreach (int dependent in dependents[readyStep.Index])
                    indegree[dependent]--;
            }
        }

        return layers;
    }

    private bool RequiresSequentialOrdering(StepGeneric left, StepGeneric right)
    {
        if (RequiresPhaseOrdering(left, right))
            return true;

        if (left.ExecutionOptions.ParallelizationMode == StepParallelizationMode.DoNotParallelize || right.ExecutionOptions.ParallelizationMode == StepParallelizationMode.DoNotParallelize)
            return true;

        return HasAccessConflict(left.IOContract, right.IOContract) || SharesSerializedArtifactSetupResource(left, right);
    }

    private static bool RequiresPhaseOrdering(StepGeneric left, StepGeneric right)
    {
        if (left.Phase != right.Phase)
            return true;

        return !IsMergeablePhase(left.Phase);
    }

    private static bool IsMergeablePhase(StepExecutionPhase phase)
    {
        return phase is StepExecutionPhase.Prepare or StepExecutionPhase.Materialize;
    }

    private bool SharesSerializedArtifactSetupResource(StepGeneric left, StepGeneric right)
    {
        string? leftResourceKey = TryGetSerializedArtifactSetupResourceKey(left);
        if (leftResourceKey is null)
            return false;

        string? rightResourceKey = TryGetSerializedArtifactSetupResourceKey(right);
        return rightResourceKey is not null && StringComparer.Ordinal.Equals(leftResourceKey, rightResourceKey);
    }

    private string? TryGetSerializedArtifactSetupResourceKey(StepGeneric step)
    {
        if (step is not SetupArtifactStep setupArtifactStep)
            return null;

        ArtifactInstanceGeneric artifactInstance = artifactStore.GetArtifact(setupArtifactStep.Identifier);
        string? resourceKey = artifactInstance.Artifact.GetSetupParallelizationResourceKey(artifactInstance);
        return resourceKey is null ? null : $"artifact-setup:{resourceKey}";
    }

    private static bool HasAccessConflict(StepIOContract left, StepIOContract right)
    {
        foreach (StepIOEntry leftOutput in left.Outputs)
        {
            if (ContainsEntry(right.Inputs, leftOutput) || ContainsEntry(right.Outputs, leftOutput))
                return true;
        }

        foreach (StepIOEntry leftInput in left.Inputs)
        {
            if (ContainsEntry(right.Outputs, leftInput))
                return true;
        }

        return false;
    }

    private static bool ContainsEntry(IEnumerable<StepIOEntry> entries, StepIOEntry candidate)
    {
        return entries.Any(x => x.Kind == candidate.Kind && StringComparer.Ordinal.Equals(x.Key, candidate.Key));
    }

    internal sealed record ScheduledStep(int Index, StepInstanceGeneric Step);
}