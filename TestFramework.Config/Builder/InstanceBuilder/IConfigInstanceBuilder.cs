using TestFramework.Config.Builder.InstanceBuilder.Actions;

namespace TestFramework.Config.Builder.InstanceBuilder;

public interface IConfigInstanceBuilder :
    IBuildAction,
    IOverrideConfigAction,
    IAddServiceAction;