namespace WebUIDoctor.Core;

public enum RepairTrigger
{
    Manual,
    AfterRecovery,
}

/// <summary>What the game says about pausing, read after a UI repair.</summary>
/// <param name="IsWorldPaused">BattleLogicWorld.IsPause: the simulation is held.</param>
/// <param name="IsCameraPaused">CameraManager.isTimePaused: the view thinks time is stopped.</param>
/// <param name="IsRenderFrozen">CameraManager is showing a still frame instead of the live world.</param>
/// <param name="IsSpeedPaused">The time-speed control itself is set to pause, which is the player's choice.</param>
/// <param name="IsInLegitPauseModal">The game believes a pausing window is still open.</param>
/// <param name="UserUiPauseHolds">How many open windows the game thinks are holding the pause.</param>
public readonly record struct PauseSnapshot(
    bool IsWorldPaused,
    bool IsCameraPaused,
    bool IsRenderFrozen,
    bool IsSpeedPaused,
    bool IsInLegitPauseModal,
    int UserUiPauseHolds)
{
    public bool IsAnythingHeld => IsWorldPaused || IsCameraPaused || IsRenderFrozen;

    public override string ToString() =>
        $"worldPaused={IsWorldPaused} cameraPaused={IsCameraPaused} renderFrozen={IsRenderFrozen} speedPaused={IsSpeedPaused} " +
        $"legitModal={IsInLegitPauseModal} uiPauseHolds={UserUiPauseHolds}";
}

public enum PauseVerdict
{
    /// <summary>Nothing is held; leave it.</summary>
    Running,

    /// <summary>The player paused with the time-speed control; resuming would override them.</summary>
    PausedByPlayer,

    /// <summary>The game says a pausing window is open; after an automatic repair that is believed.</summary>
    PausedByOpenWindow,

    /// <summary>Held with no reason the game can name: the leftover from the crashed UI. Resume it.</summary>
    Stuck,
}

/// <summary>
/// Decides whether a pause left behind after the web UI crashed should be released. The crash can leave the
/// world paused while the speed buttons still change: the window that took the pause died before it could
/// give it back. Opening and closing any game window clears it, which is what the resume does on its own.
/// </summary>
public static class PauseHeal
{
    public static PauseVerdict Decide(PauseSnapshot snapshot, RepairTrigger trigger)
    {
        if (!snapshot.IsAnythingHeld) return PauseVerdict.Running;
        if (snapshot.IsSpeedPaused) return PauseVerdict.PausedByPlayer;

        // Ctrl+F12 is only pressed when the player is stuck, and the hidden error dialog itself can count as an open window.
        // A window that holds no pause is the crashed UI's leftover: the game's own deadlock check reads holds=0 that way.
        var isWindowHoldingPause = snapshot.IsInLegitPauseModal && snapshot.UserUiPauseHolds > 0;

        if (isWindowHoldingPause && trigger == RepairTrigger.AfterRecovery) return PauseVerdict.PausedByOpenWindow;

        return PauseVerdict.Stuck;
    }
}
