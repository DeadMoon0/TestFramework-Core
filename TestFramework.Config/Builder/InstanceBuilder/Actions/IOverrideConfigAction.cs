using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace TestFramework.Config.Builder.InstanceBuilder.Actions;

public interface IOverrideConfigAction
{
    public IConfigInstanceBuilder OverrideConfig(string key, string? value);
    public IConfigInstanceBuilder OverrideConfig(IDictionary<string, string?> pairs);
    public IConfigInstanceBuilder OverrideConfig(IConfiguration configuration);
}