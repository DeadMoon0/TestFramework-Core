using System;
using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Simple;

public static class Simple
{
    public static SimpleTrigger Trigger { get; } = new SimpleTrigger();
}

public class SimpleTrigger
{
    public ActionTrigger Action(Action action)
    {
        return new ActionTrigger((_, _, _, _) => action(), [], []);
    }

    public ActionTrigger Action(Action<Dictionary<VariableIdentifier, object?>> action, params VariableReferenceGeneric[] variables)
    {
        return new ActionTrigger((_, _, vars, _) => action(vars), variables, []);
    }

    public ActionTrigger Action(Action<Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, params ArtifactIdentifier[] artifacts)
    {
        return new ActionTrigger((_, _, vars, artifacts) => action(vars, artifacts), variables, artifacts);
    }

    public ActionTrigger Action(Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, params ArtifactIdentifier[] artifacts)
    {
        return new ActionTrigger(action, variables, artifacts);
    }

    public MessageBoxTrigger MessageBox(VariableReference<string> msg, VariableReference<string> caption)
    {
        return new MessageBoxTrigger(msg, caption);
    }

    public MessageBoxTrigger MessageBox(VariableReference<string> msg)
    {
        return MessageBox(msg, "MessageBox");
    }
}