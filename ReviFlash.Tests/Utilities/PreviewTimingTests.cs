using ReviFlash.Views;

namespace ReviFlash.Tests.Utilities;

public class PreviewTimingTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(16, false)]
    [InlineData(17, true)]
    [InlineData(120, true)]
    public void Only_a_preview_slower_than_a_frame_waits_for_a_pause(int lastBuildMs, bool waits)
    {
        Assert.Equal(waits, MathText.WaitsForPause(TimeSpan.FromMilliseconds(lastBuildMs)));
    }
}
