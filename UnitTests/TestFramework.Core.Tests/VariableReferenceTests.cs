using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

public class VariableReferenceTests
{
    [Fact]
    public void UntypedTransform_SingleArg_ProjectsConstValue()
    {
        VariableStore store = CreateStore();
        VariableReferenceGeneric reference = Var.Const("abc");

        VariableReferenceGeneric transformed = reference.Transform(value => ((string?)value)?.ToUpperInvariant());

        Assert.Equal("ABC", transformed.GetValueGeneric(store));
    }

    [Fact]
    public void UntypedTransform_SingleArg_ProjectsStoreBackedValue()
    {
        VariableStore store = CreateStore();
        store.SetVariable("user", "bob");
        VariableReferenceGeneric reference = Var.Ref<string>("user");

        VariableReferenceGeneric transformed = reference.Transform(value => ((string?)value)?.ToUpperInvariant());

        Assert.Equal("BOB", transformed.GetValueGeneric(store));
    }

    [Fact]
    public void UntypedTransform_TwoArg_ProjectsConstValueWithDependencies()
    {
        VariableStore store = CreateStore();
        store.SetVariable("suffix", "!");
        VariableReferenceGeneric reference = Var.Const("abc");

        VariableReferenceGeneric transformed = reference.Transform(
            (value, resolved) => $"{value}{resolved[0]}",
            Var.Ref<string>("suffix"));

        Assert.Equal("abc!", transformed.GetValueGeneric(store));
    }

    [Fact]
    public void UntypedTransform_TwoArg_ProjectsStoreBackedValueWithDependencies()
    {
        VariableStore store = CreateStore();
        store.SetVariable("user", "bob");
        store.SetVariable("suffix", "!");
        VariableReferenceGeneric reference = Var.Ref<string>("user");

        VariableReferenceGeneric transformed = reference.Transform(
            (value, resolved) => $"{value}{resolved[0]}",
            Var.Ref<string>("suffix"));

        Assert.Equal("bob!", transformed.GetValueGeneric(store));
    }

    [Fact]
    public void ConstTransform_SingleArgAfterTwoArg_KeepsTheWholeChain()
    {
        VariableStore store = CreateStore();
        store.SetVariable("suffix", "-x");

        VariableReference<string> chained = Var.Const("abc")
            .Transform((value, resolved) => $"{value}{resolved[0]}", Var.Ref<string>("suffix"))
            .Transform(value => value?.ToUpperInvariant());

        Assert.Equal("ABC-X", chained.GetValue(store));
    }

    [Fact]
    public void ConstTransform_SingleArg_IsEvaluatedLazilyInGetValue()
    {
        VariableStore store = CreateStore();
        int invocations = 0;

        VariableReference<string> transformed = Var.Const("abc")
            .Transform(value =>
            {
                invocations++;
                return value?.ToUpperInvariant();
            });

        Assert.Equal(0, invocations);
        Assert.Equal("ABC", transformed.GetValue(store));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void ResolvableTransform_SingleArgAfterTwoArg_KeepsTheWholeChain()
    {
        VariableStore store = CreateStore();
        store.SetVariable("user", "bob");
        store.SetVariable("suffix", "-x");

        VariableReference<string> chained = Var.Ref<string>("user")
            .Transform((value, resolved) => $"{value}{resolved[0]}", Var.Ref<string>("suffix"))
            .Transform(value => value?.ToUpperInvariant());

        Assert.Equal("BOB-X", chained.GetValue(store));
    }

    private static VariableStore CreateStore()
    {
        ScopedLogger logger = new(null);
        DebuggingRunSession session = new(new EmptyRunDebugger());
        return new VariableStore(logger, session);
    }
}
