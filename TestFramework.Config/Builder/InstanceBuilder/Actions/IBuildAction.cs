using System;

namespace TestFramework.Config.Builder.InstanceBuilder.Actions;

public interface IBuildAction
{
    public ConfigInstance Build();
    public IServiceProvider BuildServiceProvider();
}