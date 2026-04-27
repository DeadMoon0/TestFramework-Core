using TestFramework.Config.Builder.InstanceBuilder.Actions;

namespace TestFramework.Config.Builder.InstanceBuilder;

/// <summary>
/// Defines the fluent configuration builder surface for creating <see cref="ConfigInstance"/> objects.
/// </summary>
public interface IConfigInstanceBuilder :
    IBuildAction,
    IOverrideConfigAction,
    IAddServiceAction;