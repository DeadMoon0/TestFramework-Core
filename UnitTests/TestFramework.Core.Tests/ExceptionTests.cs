using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines;
using Xunit;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Steps.Options;
using TestFramework.LocalIO;
using TestFramework.LocalIO.Artifacts;

namespace TestFramework.Core.Tests;

/// <summary>
/// Unit tests for friendly error messages and exception handling.
/// Verifies that framework errors guide developers toward solutions.
/// </summary>
public class ExceptionTests
{
    [Fact]
    public void MissingVariableException_IncludesAvailableVariables()
    {
        var available = new Dictionary<string, object?>
        {
            { "userId", "123" },
            { "sessionId", "abc" }
        };

        var ex = new MissingVariableException("orderId", available);

        Assert.Contains("'orderId' was never set", ex.Message);
        Assert.Contains("userId", ex.Message);
        Assert.Contains("sessionId", ex.Message);
        Assert.Contains("SetVariable", ex.Message);
        Assert.NotEmpty(ex.AvailableVariables);
    }

    [Fact]
    public void MissingVariableException_FriendlyMessage_IsActionable()
    {
        var available = new Dictionary<string, object?>
        {
            { "foo", 123 }
        };

        var ex = new MissingVariableException("bar", available);

        Assert.NotNull(ex.FriendlyMessage);
        Assert.NotEmpty(ex.RecoverySteps);
        Assert.Contains("SetVariable", ex.RecoverySteps[0]);
    }

    [Fact]
    public void MissingVariableException_EmptyVariables_StillHelpful()
    {
        var available = new Dictionary<string, object?>();

        var ex = new MissingVariableException("userId", available);

        Assert.Contains("No variables defined yet", ex.Message);
    }

    [Fact]
    public void EnvironmentNotSetException_ListsAvailableEnvironments()
    {
        var ex = new EnvironmentNotSetException();

        Assert.Contains("No environment is configured", ex.Message);
        Assert.Contains("SetEnv", ex.Message);
        Assert.Contains("AzureExt", ex.Message);
        Assert.Contains("LocalIOExt", ex.Message);
        Assert.NotEmpty(ex.AvailableOptions);
    }

    [Fact]
    public void EnvironmentNotSetException_SuggestsRecovery()
    {
        var ex = new EnvironmentNotSetException();

        Assert.NotEmpty(ex.RecoverySteps);
        Assert.Contains("SetEnv()", ex.RecoverySteps[0]);
    }

    [Fact]
    public void ArtifactNotFoundException_ListsRegisteredArtifacts()
    {
        var registered = new[] { "response", "headers", "statusCode" };

        var ex = new ArtifactNotFoundException("bodyContent", registered);

        Assert.Contains("'bodyContent' was not declared", ex.Message);
        Assert.Contains("response", ex.Message);
        Assert.Contains("headers", ex.Message);
        Assert.Contains("statusCode", ex.Message);
    }

    [Fact]
    public void ArtifactNotFoundException_EmptyRegistry_StillHelpful()
    {
        var registered = Array.Empty<string>();

        var ex = new ArtifactNotFoundException("data", registered);

        Assert.Contains("No artifact identifiers declared yet", ex.Message);
    }

    [Fact]
    public void ArtifactDoesNotYetExistException_HasRecoveryFormatting()
    {
        var ex = new ArtifactDoesNotYetExistException("payload");

        Assert.Contains("payload", ex.Message);
        Assert.Contains("Recovery:", ex.Message);
        Assert.Contains("[FRAMEWORK ERROR] ArtifactDoesNotYetExistException", ex.ToString());
    }

    [Fact]
    public void ArtifactCountMismatchException_HasActionableMessage()
    {
        var ex = new ArtifactCountMismatchException(1, 2);

        Assert.Contains("expected 1 artifact name", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FindArtifacts(baseName, ...)", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ArtifactIdentifierRequiredException_HasActionableMessage()
    {
        var ex = new ArtifactIdentifierRequiredException("FindArtifactsAs(...)");

        Assert.Contains("FindArtifactsAs", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FindArtifact(name, ...)", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ArtifactResolutionInvariantException_IncludesOperationAndVersion_WhenProvided()
    {
        var ex = new ArtifactResolutionInvariantException("payload", "artifact version capture", "latest");

        Assert.Contains("payload", ex.Message);
        Assert.Contains("artifact version capture", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("latest", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StepLabelNotFoundException_ListsAvailableLabels()
    {
        var ex = new StepLabelNotFoundException("missing", new[] { "prepare", "act" });

        Assert.Contains("missing", ex.Message);
        Assert.Contains("prepare", ex.ToString());
        Assert.Contains("act", ex.ToString());
    }

    [Fact]
    public void VariableResolvedToNullException_IncludesVariableName()
    {
        var ex = new VariableResolvedToNullException("payload", "The step requires a non-null payload.");

        Assert.Contains("payload", ex.Message);
        Assert.Contains("non-null", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArtifactVersionNotFoundException_ListsAvailableVersions()
    {
        var ex = new ArtifactVersionNotFoundException("payload", "v2", ["default", "v1"]);

        Assert.Contains("payload", ex.Message);
        Assert.Contains("v2", ex.Message);
        Assert.Contains("default", ex.ToString());
        Assert.Contains("v1", ex.ToString());
    }

    [Fact]
    public void ArtifactTypeMismatchException_ExplainsRequestedAndActualTypes()
    {
        var ex = new ArtifactTypeMismatchException("payload", typeof(FileArtifactData), typeof(ArtifactDataGeneric));

        Assert.Contains("payload", ex.Message);
        Assert.Contains(nameof(FileArtifactData), ex.ToString());
        Assert.Contains(nameof(ArtifactDataGeneric), ex.ToString());
    }

    [Fact]
    public async Task TimelineRun_ArtifactLookup_UsesFriendlyArtifactNotFoundException()
    {
        TimelineRun run = await Timeline.Create().Build().SetupRun().RunAsync();

        ArtifactNotFoundException ex = Assert.Throws<ArtifactNotFoundException>(() => run.Artifact("missingArtifact"));

        Assert.Contains("SetupArtifact", ex.ToString());
        Assert.Contains("RegisterArtifact", ex.ToString());
        Assert.Contains("FindArtifacts", ex.ToString());
    }

    [Fact]
    public async Task TimelineRun_TypedArtifactSelection_UsesFriendlyTypeMismatchException()
    {
        TimelineRun run = await Timeline.Create()
            .SetupArtifact("file")
            .Build()
            .SetupRun()
            .AddFileArtifact("file", Path.Combine(Path.GetTempPath(), $"typed-artifact-{Guid.NewGuid():N}.txt"), "payload")
            .RunAsync();

        ArtifactTypeMismatchException ex = Assert.Throws<ArtifactTypeMismatchException>(() => run.Artifact<MismatchArtifactData>("file").Select(_ => "ignored"));

        Assert.Contains("file", ex.Message);
        Assert.Contains(nameof(FileArtifactData), ex.ToString());
    }

    [Fact]
    public async Task TimelineRun_StepLookup_UsesFriendlyStepLabelNotFoundException()
    {
        TimelineRun run = await Timeline.Create().Build().SetupRun().RunAsync();

        StepLabelNotFoundException ex = Assert.Throws<StepLabelNotFoundException>(() => run.Step("missing"));

        Assert.Contains("missing", ex.Message);
        Assert.Contains("Name(\"missing\")", ex.ToString());
    }

    [Fact]
    public void TimelineRunFailedException_EmbedsFrameworkExceptionFormatting()
    {
        TimelineRunFailedException ex = new([
            new FailedStepInfo("Main", "Find Artifact", new ArtifactCountMismatchException(1, 2))
        ]);

        Assert.Contains("[FRAMEWORK ERROR] ArtifactCountMismatchException", ex.Message);
        Assert.Contains("Recovery:", ex.Message);
    }

    [Fact]
    public void ArtifactInstance_VersionLookup_UsesFriendlyVersionNotFoundException()
    {
        ArtifactInstance<FileArtifactDescriber, FileArtifactData, FileArtifactReference> instance = new(
            new FileArtifactDescriber(),
            "file",
            new FileArtifactReference("sample.txt"),
            new FileArtifactData([1, 2, 3]));

        ArtifactVersionNotFoundException ex = Assert.Throws<ArtifactVersionNotFoundException>(() =>
        {
            _ = instance["missing-version"];
        });

        Assert.Contains("missing-version", ex.Message);
        Assert.Contains("Capture version", ex.ToString());
    }

    [Fact]
    public async Task TimelineRun_RegisterArtifact_UsesFriendlyResolutionInvariantException()
    {
        TimelineRun run = await Timeline.Create()
            .RegisterArtifact("broken", new BrokenArtifactReference(BrokenArtifactReferenceMode.ReturnNullData))
            .Build()
            .SetupRun()
            .RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

        Assert.Contains(exception.FailedSteps, step =>
            step.StepException is ArtifactResolutionInvariantException invariantException &&
            invariantException.Message.Contains("artifact registration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TimelineRun_CaptureArtifactVersion_UsesFriendlyResolutionInvariantException()
    {
        TimelineRun run = await Timeline.Create()
            .CaptureArtifactVersion("broken", "v2")
            .Build()
            .SetupRun()
            .AddArtifact("broken", new BrokenArtifactReference(BrokenArtifactReferenceMode.ReturnNullData), new BrokenArtifactData())
            .RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

        Assert.Contains(exception.FailedSteps, step =>
            step.StepException is ArtifactResolutionInvariantException invariantException &&
            invariantException.Message.Contains("artifact version capture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TimelineRun_FindArtifact_UsesFriendlyResolutionInvariantException()
    {
        TimelineRun run = await Timeline.Create()
            .FindArtifact("broken", new BrokenArtifactFinder())
            .Build()
            .SetupRun()
            .RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

        Assert.Contains(exception.FailedSteps, step =>
            step.StepException is ArtifactResolutionInvariantException invariantException &&
            invariantException.Message.Contains("artifact discovery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfigurationInvalidException_ShowsExpectedFormat()
    {
        var ex = new ConfigurationInvalidException(
            "TIMEOUT",
            "integer (milliseconds)",
            "not_a_number");

        Assert.Contains("TIMEOUT", ex.Message);
        Assert.Contains("integer (milliseconds)", ex.Message);
        Assert.Contains("not_a_number", ex.Message);
    }

    [Fact]
    public void ConfigurationInvalidException_SuggestsCorrectFormat()
    {
        var ex = new ConfigurationInvalidException(
            "MAX_CONCURRENT",
            "positive integer",
            null);

        Assert.NotEmpty(ex.RecoverySteps);
        Assert.Contains("positive integer", ex.RecoverySteps[0]);
    }

    [Fact]
    public void TimelineFrameworkException_ToString_IsReadable()
    {
        var available = new Dictionary<string, object?> { { "x", 1 } };
        var ex = new MissingVariableException("y", available);

        var str = ex.ToString();

        Assert.Contains("[FRAMEWORK ERROR]", str);
        Assert.Contains("Recovery:", str);
        Assert.Contains("Available:", str);
    }

    [Fact]
    public void TimelineFrameworkException_HasConsistentFormat()
    {
        var ex1 = new EnvironmentNotSetException();
        var ex2 = new ConfigurationInvalidException("KEY", "format");

        var str1 = ex1.ToString();
        var str2 = ex2.ToString();

        // Both should use same error format
        Assert.Contains("[FRAMEWORK ERROR]", str1);
        Assert.Contains("[FRAMEWORK ERROR]", str2);
        Assert.Contains("Recovery:", str1);
        Assert.Contains("Recovery:", str2);
    }

    [Fact]
    public void IOContractViolationException_IncludesFriendlyMessage()
    {
        var missingVariable = new StepIOEntry("userId", StepIOKind.Variable);
        var precedingSteps = new[] { "step1", "step2" };
        var availableKeys = new[] { "orderId", "sessionId" };
        var similarKeys = new[] { "user_id" };

        var ex = new IOContractViolationException(
            "CheckUserStep",
            missingVariable,
            2,
            precedingSteps,
            availableKeys,
            similarKeys);

        Assert.Contains("'userId'", ex.FriendlyMessage);
        Assert.Contains("CheckUserStep", ex.FriendlyMessage);
        Assert.NotEmpty(ex.RecoverySteps);
        Assert.NotEmpty(ex.AvailableOptions);
    }

    [Fact]
    public void IOContractViolationException_RecoveryGuidanceIsActionable()
    {
        var missingArtifact = new StepIOEntry("response", StepIOKind.Artifact);
        var precedingSteps = Array.Empty<string>();
        var availableKeys = new[] { "request" };
        var similarKeys = Array.Empty<string>();

        var ex = new IOContractViolationException(
            "ValidateResponseStep",
            missingArtifact,
            1,
            precedingSteps,
            availableKeys,
            similarKeys);

        // Should suggest recovery actions
        Assert.True(ex.RecoverySteps.Count > 0, "Should have recovery steps");
        Assert.True(ex.RecoverySteps[0].Contains("response") || ex.RecoverySteps[0].Contains("artifact"), "First recovery step should mention the artifact");
    }

    [Fact]
    public void IOContractViolationException_SuggestsSimilarKeys()
    {
        var missingVariable = new StepIOEntry("totalPrice", StepIOKind.Variable);
        var availableKeys = new[] { "itemPrice", "taxAmount" };
        var similarKeys = new[] { "total_amount", "final_amount" };

        var ex = new IOContractViolationException(
            "CalculateStep",
            missingVariable,
            0,
            Array.Empty<string>(),
            availableKeys,
            similarKeys);

        // Should list similar suggestions
        var optionsText = string.Join(" ", ex.AvailableOptions);
        Assert.Contains("total_amount", optionsText);
        Assert.Contains("final_amount", optionsText);
    }

    [Fact]
    public void IOContractTypeViolationException_IncludesFriendlyMessage()
    {
        var input = new StepIOEntry("count", StepIOKind.Variable, DeclaredType: typeof(int));

        var ex = new IOContractTypeViolationException(
            "ProcessCountStep",
            input,
            typeof(string),
            "ProducerStep",
            false);

        Assert.Contains("ProcessCountStep", ex.FriendlyMessage);
        Assert.Contains("count", ex.FriendlyMessage);
        Assert.Contains("Int32", ex.FriendlyMessage);
        Assert.Contains("String", ex.FriendlyMessage);
        Assert.NotEmpty(ex.RecoverySteps);
    }

    [Fact]
    public void IOContractTypeViolationException_RecoveryGuidanceIsSpecific()
    {
        var input = new StepIOEntry("timeout", StepIOKind.Variable, DeclaredType: typeof(TimeSpan));

        var ex = new IOContractTypeViolationException(
            "WaitStep",
            input,
            typeof(int),
            "SetTimeoutStep",
            false);

        // Should suggest fixing the producer step or using Transform
        var recoveryText = string.Join(" ", ex.RecoverySteps);
        Assert.True(
            recoveryText.Contains("SetTimeoutStep") || recoveryText.Contains("Transform"),
            "Recovery should mention producer step or Transform");
    }

    [Fact]
    public void IOContractTypeViolationException_ExternalInputRecovery()
    {
        var input = new StepIOEntry("data", StepIOKind.Variable, DeclaredType: typeof(List<string>));

        var ex = new IOContractTypeViolationException(
            "ProcessDataStep",
            input,
            typeof(string),
            null,
            true);

        // For external inputs, recovery should be about the external input type
        var recoveryText = string.Join(" ", ex.RecoverySteps);
        Assert.True(
            recoveryText.Contains("external") || recoveryText.Contains("ensure"),
            "Recovery should mention external input responsibility");
    }

    [Fact]
    public void IOContractExceptions_FollowFrameworkFormat()
    {
        var input = new StepIOEntry("x", StepIOKind.Variable);

        var ex1 = new IOContractViolationException(
            "Step1", input, 0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        var ex2 = new IOContractTypeViolationException(
            "Step2", input, typeof(int), null, true);

        var str1 = ex1.ToString();
        var str2 = ex2.ToString();

        // Both should follow framework error format
        Assert.Contains("[FRAMEWORK ERROR]", str1);
        Assert.Contains("[FRAMEWORK ERROR]", str2);
        Assert.Contains("Recovery:", str1);
        Assert.Contains("Recovery:", str2);
        Assert.Contains("Available:", str1);
        Assert.Contains("Available:", str2);
    }

    private sealed class MismatchArtifactReference : ArtifactReference<MismatchArtifactReference, MismatchArtifactDescriber, MismatchArtifactData>
    {
        public override Task<ArtifactResolveResult<MismatchArtifactDescriber, MismatchArtifactData, MismatchArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => throw new NotSupportedException();

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
        {
        }

        public override string ToString() => nameof(MismatchArtifactReference);
    }

    private sealed class MismatchArtifactData : ArtifactData<MismatchArtifactData, MismatchArtifactDescriber, MismatchArtifactReference>
    {
        public override string ToString() => nameof(MismatchArtifactData);
    }

    private sealed class MismatchArtifactDescriber : ArtifactDescriber<MismatchArtifactDescriber, MismatchArtifactData, MismatchArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, MismatchArtifactData data, MismatchArtifactReference reference, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, MismatchArtifactReference reference, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override string ToString() => nameof(MismatchArtifactDescriber);
    }

    private enum BrokenArtifactReferenceMode
    {
        ReturnNullData,
        ReturnConcreteData
    }

    private sealed class BrokenArtifactFinder : ArtifactFinder<BrokenArtifactDescriber, BrokenArtifactData, BrokenArtifactReference>
    {
        public override Task<ArtifactFinderResult?> FindAsync(IServiceProvider serviceProvider, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger, System.Threading.CancellationToken cancellationToken)
            => Task.FromResult<ArtifactFinderResult?>(new ArtifactFinderResult(new BrokenArtifactReference(BrokenArtifactReferenceMode.ReturnNullData)));

        public override Task<ArtifactFinderResultMulti> FindMultiAsync(IServiceProvider serviceProvider, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger, System.Threading.CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class BrokenArtifactReference(BrokenArtifactReferenceMode mode) : ArtifactReference<BrokenArtifactReference, BrokenArtifactDescriber, BrokenArtifactData>
    {
        public override Task<ArtifactResolveResult<BrokenArtifactDescriber, BrokenArtifactData, BrokenArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<BrokenArtifactDescriber, BrokenArtifactData, BrokenArtifactReference>
            {
                Found = true,
                Data = mode == BrokenArtifactReferenceMode.ReturnConcreteData ? new BrokenArtifactData() : null
            });

        public override void DeclareIO(StepIOContract contract)
        {
        }

        public override void OnPinReference(TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
        {
        }

        public override string ToString() => nameof(BrokenArtifactReference);
    }

    private sealed class BrokenArtifactData : ArtifactData<BrokenArtifactData, BrokenArtifactDescriber, BrokenArtifactReference>
    {
        public override string ToString() => nameof(BrokenArtifactData);
    }

    private sealed class BrokenArtifactDescriber : ArtifactDescriber<BrokenArtifactDescriber, BrokenArtifactData, BrokenArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, BrokenArtifactData data, BrokenArtifactReference reference, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, BrokenArtifactReference reference, TestFramework.Core.Variables.VariableStore variableStore, TestFramework.Core.Logging.ScopedLogger logger)
            => Task.CompletedTask;

        public override string ToString() => nameof(BrokenArtifactDescriber);
    }
}
