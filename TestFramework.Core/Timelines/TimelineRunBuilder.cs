using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;
using TestFramework.Core.Logging.BuildInEvents;
using TestFramework.Core.Runner;
using TestFramework.Core.Environment;
using TestFramework.Core.Steps.SystemSteps;
using Xunit.Abstractions;

namespace TestFramework.Core.Timelines;

internal class TimelineRunBuilder : ITimelineRunBuilder
{
    private readonly ScopedLogger logger;
    private readonly Timeline _timeline;
    private readonly PreProcessableStage _mainStage;
    private readonly IServiceProvider _serviceProvider;
    private readonly ArtifactStore _newArtifactStore;
    private readonly VariableStore _newVariableStore;
    private readonly EnvComponentContext _environmentContext;

    private readonly DebuggingRunSession _debuggingSession;

    /// <summary>
    /// The debuggers CommonDebugger constructed for this run. Releasing them at the end of the run
    /// closes the pipe handle a connected UI would otherwise leak once per run.
    /// </summary>
    private readonly IDisposable? _ownedDebuggerResources;

    private readonly List<VariableIdentifier> _externalVariables = [];
    private readonly List<ArtifactIdentifier> _externalArtifacts = [];
    private IEnvironmentProvider? _environment;

    /// <summary>
    /// Set as soon as a run starts — before any work — so a failed run also invalidates the builder
    /// instead of inviting a retry against the same, half-populated stores.
    /// </summary>
    private bool _hasRun;

    internal TimelineRunBuilder(IServiceProvider serviceProvider, ITestOutputHelper? outputHelper, Timeline timeline, PreProcessableStage mainStage, string? sourceFilePath = null, int sourceLineNumber = 0)
    {
        _timeline = timeline;
        _mainStage = mainStage;
        _serviceProvider = serviceProvider;

        _debuggingSession = new DebuggingRunSession(
            CommonDebugger.GetCommon(_serviceProvider, outputHelper, out _ownedDebuggerResources),
            sourceFilePath,
            sourceLineNumber);

        // Resolved here rather than at the first signal: this constructor still runs on the
        // caller's stack, and by the time signalling starts a single await may have removed the
        // test method from it.
        _debuggingSession.CaptureIdentity(sourceFilePath, sourceLineNumber);
        logger = ScopedLogger.CreateWithDebuggerSession(_debuggingSession);

        _newArtifactStore = new ArtifactStore(logger, _debuggingSession);
        _newVariableStore = new VariableStore(logger, _debuggingSession);
        _environmentContext = new EnvComponentContext();
    }

    private void EnsureNotAlreadyUsed(string operation)
    {
        if (_hasRun) throw new TimelineRunBuilderAlreadyUsedException(operation);
    }

    public async Task<TimelineRun> RunAsync()
    {
        EnsureNotAlreadyUsed(nameof(RunAsync));
        _hasRun = true;

        IServiceProvider runServiceProvider = _environment is IRunScopedServiceProviderFactory scopedServiceProviderFactory
            ? scopedServiceProviderFactory.CreateRunScopedServiceProvider(_serviceProvider)
            : _serviceProvider;

        FreezableCollection<StageInstance> stages = PreProcessStages(_newArtifactStore, _newVariableStore, out IReadOnlyList<StepGeneric> mainStageSteps, out VariableTracker variableTracker);
        IOContractValidator.Validate(mainStageSteps, _externalVariables, _externalArtifacts, variableTracker);
        TimelineRun newRun = new TimelineRun(_timeline, stages, _newArtifactStore, _newVariableStore, _environmentContext, logger);

        await _debuggingSession.InitSessionAsync(BuildRunStructure(newRun));
        await _debuggingSession.TransitionRunAsync(DebugLifecycleState.Initialized);

        var coreRunner = new CoreRunner();
        var totalStopwatch = Stopwatch.StartNew();
        bool runTransitionCompleted = false;
        try
        {
            await _debuggingSession.TransitionRunAsync(DebugLifecycleState.Running, DebugLifecycleState.Initialized);
            logger.Log(new TimelineRunHeaderLogEvent(
                DateTime.UtcNow,
                [.. _newVariableStore.GetAll().Select(entry => (entry.Key, entry.Value))],
                [.. _newArtifactStore.GetAll().Select(instance => (instance.Identifier, instance))],
                newRun.Stages,
                mainStageSteps));
            foreach (var stage in newRun.Stages)
            {
                // A cancelled run skips the work it has not started but always reaches teardown:
                // deconstructing artifacts and tearing down environment components is precisely what
                // killing the process would have skipped.
                if (_debuggingSession.IsCancellationRequested && !stage.Stage.IsCleanupStage)
                {
                    await _debuggingSession.TransitionStageAsync(stage.Stage.Name, DebugLifecycleState.Skipped, DebugLifecycleState.Initialized);
                    continue;
                }

                await _debuggingSession.TransitionStageAsync(stage.Stage.Name, DebugLifecycleState.Running, DebugLifecycleState.Initialized);
                logger.Log(new EnterStageLogEvent(stage));

                using var _ = logger.EnterIndentScope();
                var stageStopwatch = Stopwatch.StartNew();
                await coreRunner.RunStage(stage, runServiceProvider, logger, newRun.VariableStore, newRun.ArtifactStore, _debuggingSession);
                stageStopwatch.Stop();
                logger.Log(new StageSummaryLogEvent(stage, stageStopwatch.Elapsed));
                await _debuggingSession.TransitionStageAsync(stage.Stage.Name, stage.Result.State == StageState.Complete ? DebugLifecycleState.Complete : DebugLifecycleState.Error, DebugLifecycleState.Running);
            }
            DebugLifecycleState finalRunState = newRun.Stages.Any(stage => stage.Result.State == StageState.Error)
                ? DebugLifecycleState.Error
                : DebugLifecycleState.Complete;
            await _debuggingSession.TransitionRunAsync(finalRunState, DebugLifecycleState.Running);
            runTransitionCompleted = true;
        }
        finally
        {
            totalStopwatch.Stop();
            if (!runTransitionCompleted)
                await _debuggingSession.TransitionRunAsync(DebugLifecycleState.Error, DebugLifecycleState.Running);
            await _debuggingSession.FinishSessionAsync();
            _ownedDebuggerResources?.Dispose();
        }
        newRun.Freeze();
        return newRun;
    }

    /// <summary>
    /// Builds the structural snapshot handed to debuggers at the start of a run.
    /// </summary>
    /// <remarks>
    /// This serializes every seeded variable and artifact plus the whole step graph. When no debugger
    /// is capturing it is pure waste, so an empty structure is returned instead.
    /// </remarks>
    private TimelineRunStructure BuildRunStructure(TimelineRun newRun)
    {
        if (!_debuggingSession.IsCapturing)
        {
            return new TimelineRunStructure
            {
                Artifacts = new Dictionary<ArtifactIdentifier, Debugger.ArtifactState>(),
                Variables = new Dictionary<VariableIdentifier, VariableState>(),
                Stages = []
            };
        }

        return new TimelineRunStructure
        {
            Artifacts = _newArtifactStore.GetAll().ToDictionary(x => x.Identifier, ArtifactStore.GetDebuggingStateFromInstance),
            Variables = _newVariableStore.GetAll().ToDictionary(x => x.Key, x => VariableStore.GetDebuggingStateFromValue(x.Value, x.Key)),
            Stages = [.. newRun.Stages.Select(stage => BuildStageStructure(stage))]
        };
    }

    private DebugStageState BuildStageStructure(StageInstance stage)
    {
        IReadOnlyDictionary<int, int> layerByStepIndex = BuildLayerMap(stage);

        return new DebugStageState
        {
            Name = stage.Stage.Name,
            Description = stage.Stage.Description,
            Steps = [.. stage.Stage.Steps.Select((step, index) => new DebugStepState
            {
                Name = step.Name,
                Description = step.Description,
                DoesReturn = step.DoesReturn,
                ErrorHandlingOptions = step.ErrorHandlingOptions,
                ExecutionOptions = step.ExecutionOptions,
                IOContract = step.IOContract,
                Phase = step.Phase,
                LabelOptions = step.LabelOptions,
                RetryOptions = step.RetryOptions,
                TimeOutOptions = step.TimeOutOptions,
                LayerIndex = layerByStepIndex.TryGetValue(index, out int layer) ? layer : 0,
            })]
        };
    }

    /// <summary>
    /// Runs the planner to find out which steps will execute together.
    /// </summary>
    /// <remarks>
    /// The same planner the runner uses, over the same stage, so the layers reported are the layers
    /// that run — not a second implementation that could drift from it. Step indices line up because
    /// <see cref="StageInstance.Steps"/> is built one-to-one from the stage's steps.
    /// <para>
    /// A stage whose graph cannot be planned still has to be describable: the run is about to fail
    /// with that error anyway, and a debugger that throws while preparing to report the failure would
    /// replace a diagnosable problem with an undiagnosable one.
    /// </para>
    /// </remarks>
    private IReadOnlyDictionary<int, int> BuildLayerMap(StageInstance stage)
    {
        Dictionary<int, int> layers = [];

        try
        {
            IReadOnlyList<IReadOnlyList<Runner.StageExecutionPlanner.ScheduledStep>> plan =
                new Runner.StageExecutionPlanner(stage, _newArtifactStore).BuildLayers();

            for (int layerIndex = 0; layerIndex < plan.Count; layerIndex++)
            {
                foreach (Runner.StageExecutionPlanner.ScheduledStep scheduled in plan[layerIndex])
                    layers[scheduled.Index] = layerIndex;
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }

        return layers;
    }

    private FreezableCollection<StageInstance> PreProcessStages(ArtifactStore artifactStore, VariableStore variableStore, out IReadOnlyList<StepGeneric> mainStageSteps, out VariableTracker trackedVariables)
    {
        Stage preSetupStage = new Stage()
        {
            Name = "Pre-Setup Stage",
            Description = "Preparatory steps that must complete before the Main Stage begins (e.g. creating temporary subscriptions).",
        };
        Stage mainStage = new Stage()
        {
            Name = _mainStage.Name,
            Description = _mainStage.Description,
        };
        Stage cleanUpStage = new Stage()
        {
            Name = "Cleanup Stage",
            Description = "The Stage where all Cleanup Steps are Executed.",
            IsCleanupStage = true,
        };

        var artifactTracker = new ArtifactTracker();
        var variableTracker = new VariableTracker();
        trackedVariables = variableTracker;

        List<StepGeneric> bufferedPreSetupSteps = [];
        List<StepGeneric> bufferedMainSteps = [];
        List<StepGeneric> bufferedCleanupSteps = [];

        using (var _ = logger.EnterIndentScope())
        {
            foreach (var stepEmitter in _mainStage.Steps)
            {
                foreach (var step in stepEmitter.Emit(artifactStore, variableStore, variableTracker, artifactTracker, null))
                {
                    step.Step.DeclareIO(step.Step.IOContract);
                    foreach (ResultBinding binding in step.Step.ResultOptions.ResultBindings)
                    {
                        if (!step.Step.IOContract.Outputs.Any(output => output.Key == binding.Variable.Identifier && output.Kind == StepIOKind.Variable))
                            step.Step.IOContract.Outputs.Add(new StepIOEntry(binding.Variable.Identifier, StepIOKind.Variable, false, binding.DeclaredType));
                    }
                    if (step.RedirectToCleanUp)
                        bufferedCleanupSteps.Add(step.Step);
                    else if (step.RunInPreSetupStage)
                        bufferedPreSetupSteps.Add(step.Step);
                    else
                        bufferedMainSteps.Add(step.Step);
                }
            }
        }

        IReadOnlyCollection<EnvironmentRequirement> environmentRequirements = CollectEnvironmentRequirements(bufferedMainSteps, variableStore);

        if (_environment is not null)
            preSetupStage.Steps.Add(new CreateEnvComponentsStep(_environment, _environmentContext, environmentRequirements));

        foreach (StepGeneric step in bufferedPreSetupSteps)
            preSetupStage.Steps.Add(step);
        foreach (StepGeneric step in bufferedMainSteps)
            mainStage.Steps.Add(step);
        foreach (StepGeneric step in bufferedCleanupSteps)
            cleanUpStage.Steps.Add(step);

        // Always append DeconstructAllArtifactsStep as the very last cleanup step,
        // after all IHasCleanupStep contributions. Errors are ignored so cleanup
        // continues even if individual artifact deconstruction fails.
        var deconstructStep = new DeconstructAllArtifactsStep();
        deconstructStep.ErrorHandlingOptions.IgnoreExceptionTypes.Add(typeof(Exception));
        cleanUpStage.Steps.Add(deconstructStep);

        if (_environment is not null)
        {
            var deconstructEnvStep = new DeconstructEnvComponentsStep(_environment, _environmentContext);
            deconstructEnvStep.ErrorHandlingOptions.IgnoreExceptionTypes.Add(typeof(Exception));
            cleanUpStage.Steps.Add(deconstructEnvStep);
        }

        mainStageSteps = mainStage.Steps.ToList();

        preSetupStage.Freeze();
        mainStage.Freeze();
        cleanUpStage.Freeze();

        var stageInstances = new FreezableCollection<StageInstance>();
        // Only include Pre-Setup Stage when it actually has steps — keeps the output clean for tests that don't need it.
        if (preSetupStage.Steps.Count > 0)
            stageInstances.Add(new StageInstance(preSetupStage));
        stageInstances.Add(new StageInstance(mainStage));
        stageInstances.Add(new StageInstance(cleanUpStage));

        return stageInstances;
    }

    private static IReadOnlyCollection<EnvironmentRequirement> CollectEnvironmentRequirements(IEnumerable<StepGeneric> steps, VariableStore variableStore)
    {
        List<EnvironmentRequirement> requirements = [];
        foreach (StepGeneric step in steps)
        {
            if (step is IHasEnvironmentRequirements provider)
                requirements.AddRange(provider.GetEnvironmentRequirements(variableStore));
        }

        return requirements;
    }
    public ITimelineRunBuilder AddArtifact<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference, ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference> data)
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    {
        EnsureNotAlreadyUsed(nameof(AddArtifact));

        // Clone so a reference handed to several runs never shares its pinned state across them.
        TArtifactReference runReference = (TArtifactReference)reference.CloneForRun();
        _newArtifactStore.AddArtifact(new ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>(runReference.GetArtifactDescriber(), identifier, runReference, (TArtifactData)data));
        _externalArtifacts.Add(identifier);
        return this;
    }

    public ITimelineRunBuilder SetEnv(IEnvironmentProvider environment)
    {
        EnsureNotAlreadyUsed(nameof(SetEnv));

        if (_environment is not null)
            throw new FrameworkConfigurationException("Only one environment can be configured for a timeline run.");

        _environment = environment;
        return this;
    }

    public ITimelineRunBuilder AddVariable<T>(VariableIdentifier identifier, T value)
    {
        EnsureNotAlreadyUsed(nameof(AddVariable));

        _newVariableStore.SetVariable(identifier, value);
        _externalVariables.Add(identifier);
        return this;
    }
}
