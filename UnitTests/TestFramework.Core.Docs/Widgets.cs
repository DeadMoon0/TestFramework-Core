using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;

namespace TestFramework.Core.Docs.Widgets;

/// <summary>A step that leaves a picture of what it saw.</summary>
public sealed class TakesAScreenshot : Step<EmptyStepResultContext>
{
    public override string Name => "Takes a screenshot";

    public override string Description => "Publishes what the page looked like.";

    public override bool DoesReturn => false;

    #region snippet:widgets-publish
    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        byte[] png = await CaptureAsync();

        // Attributed to this step and this attempt; the file lands in the run's own output.
        context.Widgets.Publish(new Widget
        {
            Kind = WidgetKinds.Screenshot,
            Name = "after-login",
            Form = DebugPreviewForm.Image,
            Bytes = png,
            Summary = "The dashboard, logged in"
        });

        return EmptyStepResultContext.Instance;
    }
    #endregion

    public override Step<EmptyStepResultContext> Clone() => new TakesAScreenshot().WithClonedOptions(this);

    public override void DeclareIO(StepIOContract contract)
    {
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    private static Task<byte[]> CaptureAsync() => Task.FromResult(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
}
