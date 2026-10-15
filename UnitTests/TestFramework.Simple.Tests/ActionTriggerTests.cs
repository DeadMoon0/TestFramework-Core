using TestFramework.Core.Steps;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Simple;

namespace TestFramework.Simple.Tests;

public class ActionTriggerTests
{
    [Fact]
    public void AConstantPassedAsAVariable_IsRefusedWhenTheTimelineIsBuilt()
    {
        // The dictionary the action reads is keyed by identifier, and a constant has none to key it by.
        FrameworkConfigurationException refused = Assert.Throws<FrameworkConfigurationException>(
            () => SimpleExt.Trigger.Action(_ => { }, Var.Const("Ada")));

        Assert.Contains("Var.Ref", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameVariableNamedTwice_IsRefusedByName()
    {
        FrameworkConfigurationException refused = Assert.Throws<FrameworkConfigurationException>(
            () => SimpleExt.Trigger.Action(_ => { }, Var.Ref<string>("user"), Var.Ref<string>("user")));

        Assert.Contains("'user'", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameArtifactNamedTwice_IsRefusedByName()
    {
        FrameworkConfigurationException refused = Assert.Throws<FrameworkConfigurationException>(
            () => SimpleExt.Trigger.Action((_, _) => { }, [], "report", "report"));

        Assert.Contains("'report'", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_PassesResolvedVariablesToAction()
    {
        RunContext context = RunContext.Detached();
        VariableIdentifier identifier = new("user");
        context.Variables.SetVariable(identifier, "Ada");

        Dictionary<VariableIdentifier, object?>? captured = null;
        ActionTrigger trigger = SimpleExt.Trigger.Action(vars => captured = vars, Var.Ref<string>(identifier));

        await trigger.Execute(context);

        Assert.NotNull(captured);
        Assert.Equal("Ada", captured![identifier]);
    }

    [Fact]
    public async Task ActionFactory_CreatesExecutableTriggerForParameterlessAction()
    {
        RunContext context = RunContext.Detached();
        int callCount = 0;

        ActionTrigger trigger = SimpleExt.Trigger.Action(() => callCount++);

        await trigger.Execute(context);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ActionFactory_CreatesExecutableTriggerForArtifactAwareAction()
    {
        RunContext context = RunContext.Detached();
        bool executed = false;

        ActionTrigger trigger = SimpleExt.Trigger.Action(
            (vars, artifacts) =>
            {
                executed = true;
                Assert.Empty(vars);
                Assert.Empty(artifacts);
            },
            [],
            []);

        await trigger.Execute(context);

        Assert.True(executed);
    }

    [Fact]
    public async Task ActionFactory_CreatesExecutableTriggerForFullContextAction()
    {
        RunContext context = RunContext.Detached();
        bool executed = false;

        ActionTrigger trigger = SimpleExt.Trigger.Action(
            (serviceProvider, logger, vars, artifacts) =>
            {
                executed = true;
                Assert.Same(context.Services, serviceProvider);
                Assert.Same(context.Logger, logger);
                Assert.Empty(vars);
                Assert.Empty(artifacts);
            },
            [],
            []);

        await trigger.Execute(context);

        Assert.True(executed);
    }

    [Fact]
    public void ActionFactory_ThrowsImmediatelyForNullAction()
    {
        Assert.Throws<ArgumentNullException>(() => SimpleExt.Trigger.Action((Action)null!));
    }

    [Fact]
    public void DeclareIO_AddsVariableIdentifiers()
    {
        ActionTrigger trigger = SimpleExt.Trigger.Action(_ => { }, Var.Ref<string>("input"), Var.Ref<int>("count"));
        StepIOContract contract = new();

        trigger.DeclareIO(contract);

        Assert.Collection(
            contract.Inputs,
            entry =>
            {
                Assert.Equal("input", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
            },
            entry =>
            {
                Assert.Equal("count", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
            });
    }

}