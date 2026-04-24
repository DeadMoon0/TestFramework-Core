using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
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

    private readonly List<VariableIdentifier> _externalVariables = [];
    private readonly List<ArtifactIdentifier> _externalArtifacts = [];
    private IEnvironmentProvider? _environment;

    internal TimelineRunBuilder(IServiceProvider serviceProvider, ITestOutputHelper? outputHelper, Timeline timeline, PreProcessableStage mainStage)
    {
        logger = new ScopedLogger(outputHelper);
        logger.StartBuffering();
        _timeline = timeline;
        _mainStage = mainStage;
        _serviceProvider = serviceProvider;

        _debuggingSession = new DebuggingRunSession(((IRunDebugger?)_serviceProvider.GetService(typeof(IRunDebugger))) ?? CommonDebugger.GetCommon());

        _newArtifactStore = new ArtifactStore(logger, _debuggingSession);
        _newVariableStore = new VariableStore(logger, _debuggingSession);
        _environmentContext = new EnvComponentContext();
    }

    public async Task<TimelineRun> RunAsync()
    {
        logger.LogInformation("─ Plan ──────────────────────────────────────");
        FreezableCollection<StageInstance> stages = PreProcessStages(_newArtifactStore, _newVariableStore, out IReadOnlyList<StepGeneric> mainStageSteps);
        logger.LogInformation("─────────────────────────────────────────────");
        IOContractValidator.Validate(mainStageSteps, _externalVariables, _externalArtifacts);
        TimelineRun newRun = new TimelineRun(_timeline, stages, _newArtifactStore, _newVariableStore, _environmentContext, logger);

        await _debuggingSession.InitSessionAsync(new TimelineRunStructure
        {
            Artifacts = _newArtifactStore.GetAll().ToDictionary(x => x.Identifier, ArtifactStore.GetDebuggingStateFromInstance),
            Variables = _newVariableStore.GetAll().ToDictionary(x => x.Key, x => VariableStore.GetDebuggingStateFromValue(x.Value, x.Key)),
            Stages = [.. newRun.Stages.Select(x => new DebugStageState
            {
                Name = x.Stage.Name,
                Description = x.Stage.Description,
                Steps = [.. x.Stage.Steps.Select(x => new DebugStepState
                {
                    Name = x.Name,
                    Description = x.Description,
                    DoesReturn = x.DoesReturn,
                    ErrorHandlingOptions = x.ErrorHandlingOptions,
                    ExecutionOptions = x.ExecutionOptions,
                    IOContract = x.IOContract,
                    LabelOptions = x.LabelOptions,
                    RetryOptions = x.RetryOptions,
                    TimeOutOptions = x.TimeOutOptions,
                })]
            })]
        });

        logger.StopBuffering();
        logger.Log(new TimelineRunHeaderLogEvent(
            DateTime.Now,
            _externalVariables.Select(id => (id, _newVariableStore.GetVariable(id))).ToList(),
            _externalArtifacts.Select(id => (id, _newArtifactStore.GetArtifact(id))).ToList(),
            stages,
            mainStageSteps
        ));
        logger.FlushBuffer();

        var coreRunner = new CoreRunner();
        var totalStopwatch = Stopwatch.StartNew();
        try
        {
            foreach (var stage in newRun.Stages)
            {
                logger.LogInformation("");
                logger.Log(new EnterStageLogEvent(stage));

                await _debuggingSession.EnterStageAsync(stage.Stage.Name);

                using var _ = logger.EnterIndentScope();
                var stageStopwatch = Stopwatch.StartNew();
                await coreRunner.RunStage(stage, _serviceProvider, logger, newRun.VariableStore, newRun.ArtifactStore, _debuggingSession);
                stageStopwatch.Stop();
                logger.Log(new StageSummaryLogEvent(stage, stageStopwatch.Elapsed));
            }
        }
        finally
        {
            totalStopwatch.Stop();
            logger.LogInformation("");
            logger.Log(new FailedStepsRecapLogEvent(newRun.Stages));
            logger.Log(new TimelineRunSummaryLogEvent(newRun.Stages, totalStopwatch.Elapsed));
            await _debuggingSession.FinishSessionAsync();
        }
        newRun.Freeze();
        return newRun;
    }

    private FreezableCollection<StageInstance> PreProcessStages(ArtifactStore artifactStore, VariableStore variableStore, out IReadOnlyList<StepGeneric> mainStageSteps)
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
        };

        var artifactTracker = new ArtifactTracker();
        var variableTracker = new VariableTracker();

        List<StepGeneric> bufferedPreSetupSteps = [];
        List<StepGeneric> bufferedMainSteps = [];
        List<StepGeneric> bufferedCleanupSteps = [];

        using (var _ = logger.EnterIndentScope())
        {
            foreach (var stepEmitter in _mainStage.Steps)
            {
                foreach (var step in stepEmitter.Emit(artifactStore, variableStore, variableTracker, artifactTracker, logger))
                {
                    step.Step.DeclareIO(step.Step.IOContract);
                    if (step.Step.DoesReturn) step.Step.IOContract.Outputs.Add(new StepIOEntry("out", StepIOKind.Variable));
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

        logger.LogInformation("Stage '{0}': {1} step(s)  pre-setup: {2}  cleanup: {3}",
            _mainStage.Name, mainStage.Steps.Count, preSetupStage.Steps.Count, cleanUpStage.Steps.Count);

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
        _newArtifactStore.AddArtifact(new ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>(reference.GetArtifactDescriber(), identifier, (TArtifactReference)reference, (TArtifactData)data));
        _externalArtifacts.Add(identifier);
        return this;
    }

    public ITimelineRunBuilder SetEnv(IEnvironmentProvider environment)
    {
        if (_environment is not null)
            throw new InvalidOperationException("Only one environment can be configured for a timeline run.");

        _environment = environment;
        return this;
    }

    public ITimelineRunBuilder AddVariable<T>(VariableIdentifier identifier, T value)
    {
        _newVariableStore.SetVariable(identifier, value);
        _externalVariables.Add(identifier);
        return this;
    }
}
