using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

public class CoreRuntimeTests
{
    [Fact]
    public void FreezableDictionary_FreezePreventsMutationAndFreezesNestedValues()
    {
        FreezableDictionary<string, TestFreezable> dictionary = new();
        TestFreezable nested = new();
        dictionary.Add("child", nested);

        dictionary.Freeze();

        Assert.True(dictionary.IsFrozen);
        Assert.True(dictionary.IsReadOnly);
        Assert.True(nested.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => dictionary.Add("second", new TestFreezable()));
    }

    [Fact]
    public void FreezableCollection_CastPreservesItemsAndFrozenState()
    {
        FreezableCollection<string> source = new();
        source.Add("alpha");
        source.Add("beta");
        source.Freeze();

        IFreezableCollection<object> casted = source.Cast<object>();

        Assert.True(casted.IsFrozen);
        Assert.Equal(["alpha", "beta"], casted.Cast<object>());
    }

    [Fact]
    public void VariableStore_SetVariable_ReplacesAndRetrievesTypedValues()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        VariableIdentifier identifier = new("user");

        runtime.VariableStore.SetVariable(identifier, "Ada");
        runtime.VariableStore.SetVariable(identifier, "Grace");

        Assert.Equal("Grace", runtime.VariableStore.GetVariable<string>(identifier));
        Assert.True(runtime.VariableStore.TryGetVariable<string>(identifier, out string? resolved));
        Assert.Equal("Grace", resolved);
    }

    private sealed class RuntimeContext
    {
        public ScopedLogger Logger { get; } = new(null);
        public DebuggingRunSession DebuggingSession { get; } = new(EmptyRunDebugger.CreateNew());
        public VariableStore VariableStore { get; }

        private RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
        }

        public static RuntimeContext Create() => new();
    }

    private sealed class TestFreezable : IFreezable
    {
        public bool IsFrozen { get; private set; }

        public void Freeze()
        {
            IsFrozen = true;
        }
    }
}