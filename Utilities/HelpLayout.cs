using System;

namespace ReviFlash.Utilities;

/// <summary> How wide the Help panel is, and whether it sits beside the page or over it. </summary>
public static class HelpLayout
{
    public const double PageMinWidth = 760;
    public const double MaxWidth = 620;
    public const double MinDockedWidth = 420;

    /// <summary> Docked beside the page while both fit; otherwise floating over the page's right side. </summary>
    public static (double Width, bool Docked) Fit(double windowWidth)
    {
        var room = windowWidth - PageMinWidth;
        return room >= MinDockedWidth
            ? (Math.Min(MaxWidth, room), true)
            : (Math.Min(MaxWidth, windowWidth), false);
    }
}
