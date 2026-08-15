using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Steps.Preprocessor;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// A built timeline is shared by every run started from it, so its emitters must stop accepting
/// modifiers once Build() has returned.
/// </summary>
public class StepEmitterFreezeTests
{
    [Fact]
    public void Build_FreezesTheEmittersInTheMainStage()
    {
        Timeline timeline = Timeline.Create()
            .Trigger(new NoopStep())
            .Name("only")
            .Build();

        StepEmitter emitter = timeline.MainStage.Steps.Single();

        Assert.True(emitter.IsFrozen);
        Assert.Throws<FrameworkStateException>(() => emitter.AddModifier(static (_, _, _) => { }));
    }

    private sealed class NoopStep : Step<EmptyStepResultContext>
    {
        public override string Name => "noop";
        public override string Description => "does nothing";
        public override bool DoesReturn => false;

        public override Step<EmptyStepResultContext> Clone() => new NoopStep().WithClonedOptions(this);

        public override Task<EmptyStepResultContext?> Execute(
            IServiceProvider serviceProvider,
            VariableStore variableStore,
            ArtifactStore artifactStore,
            ScopedLogger logger,
            CancellationToken cancellationToken)
            => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);

        public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

        public override void DeclareIO(StepIOContract contract)
        {
        }
    }
}
