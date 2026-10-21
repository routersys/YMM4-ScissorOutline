using System.Windows;
using Telemetry;

namespace ScissorOutline.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        ScissorOutlineTelemetry.EnsureStartedOnce();
        ScissorOutlineTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new ScissorOutlineEffect();

        Assert.Equal(Texts.ScissorOutline, effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
