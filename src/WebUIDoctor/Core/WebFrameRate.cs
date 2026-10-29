using System;

namespace WebUIDoctor.Core;

/// <summary>
/// The game asks its browser for 30 frames a second on every quality tier (WebUILayer.GetTargetWebFrameRateForTier
/// returns 30 for Low, Medium and High), down from the browser's own 60. Every menu and the HUD then redraw and reach
/// the screen at most every 33 ms, which is what makes menus feel slow to answer the mouse while every other measure
/// looks fine. The browser takes the rate once, when it starts, so a change needs a game restart.
/// </summary>
public static class WebFrameRate
{
    public const int GameRate = 30;
    public const int Default = 60;
    public const int Min = 30;
    public const int Max = 120;

    /// <summary>The rate the browser is asked for. A hand-edited config value outside the range is pulled back into it.</summary>
    public static uint Pick(int configured) => (uint)Math.Clamp(configured, Min, Max);
}
