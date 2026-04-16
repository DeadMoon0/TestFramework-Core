using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace TestFramework.Config.Builder.InstanceBuilder.Actions;

public interface IAddServiceAction
{
    public IConfigInstanceBuilder AddService(Action<IServiceCollection, IConfiguration> action);
    public IConfigInstanceBuilder AddService(Action<IServiceCollection> action);
}