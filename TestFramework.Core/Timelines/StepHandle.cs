using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Assertions;

namespace TestFramework.Core.Timelines;

public class StepHandle
{
    private readonly StepInstanceGeneric _instance;
    private readonly ScopedLogger? _logger;

    internal StepHandle(StepInstanceGeneric instance, ScopedLogger? logger)
    {
        _instance = instance;
        _logger = logger;
    }

    public string? Label => _instance.Step.LabelOptions.Label;
    public StepState State => _instance.State;
    public StepResultGeneric LastResult => _instance.LastResult;
    public IFreezableCollection<StepResultGeneric> RetryResults => _instance.RetryResults;
    public StepGeneric Step => _instance.Step;

    public StepAsserter Should() => new StepAsserter(_instance, _logger);

    internal ScopedLogger? Logger => _logger;

    public static implicit operator StepInstanceGeneric(StepHandle handle) => handle._instance;
}
