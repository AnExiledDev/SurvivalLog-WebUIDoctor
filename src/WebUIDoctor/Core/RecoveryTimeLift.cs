namespace WebUIDoctor.Core;

/// <summary>
/// The game's UI recovery waits one second of game time before it restarts the browser (RecoverWebView, a scaled
/// UniTask.Delay). The game's pause is a time scale of 0.0001, so while paused that second takes almost three hours
/// and the UI never comes back. This runs time at normal speed until that second has passed, then puts the pause back.
/// </summary>
public abstract record LiftState
{
    /// <summary>No lift active; a recovery seen while paused gets one.</summary>
    public sealed record Ready : LiftState;

    /// <summary>Time runs at normal speed for the recovery's wait; <paramref name="SavedScale"/> goes back afterwards.</summary>
    public sealed record Lifted(float SavedScale, float ScaledElapsed) : LiftState;

    /// <summary>This recovery already had its lift; the next recovery gets a new one.</summary>
    public sealed record Spent : LiftState;

    public static LiftState Initial => new Ready();
}

public enum LiftAction
{
    None,

    /// <summary>Set the time scale to <see cref="RecoveryTimeLift.LiftedScale"/>.</summary>
    Lift,

    /// <summary>Set the time scale back to the step's <c>Scale</c>.</summary>
    Restore,

    /// <summary>Something else changed the speed while lifted; leave its choice alone.</summary>
    LeaveSpeedAlone,
}

public readonly record struct LiftStep(LiftAction Action, float Scale, LiftState Next);

public static class RecoveryTimeLift
{
    public const float LiftedScale = 1f;

    /// <summary>The game's paused scale is 0.0001; its slowest real speed is 0.3.</summary>
    public const float PausedBelow = 0.01f;

    /// <summary>The game waits 1 s; the margin covers the frame the lift lands on.</summary>
    public const float HoldForScaledSeconds = 1.25f;

    /// <param name="scaledDelta">Game time that passed this frame (Time.deltaTime), the clock the game's wait counts.</param>
    public static LiftStep Tick(LiftState state, bool isRecovering, float timeScale, float scaledDelta)
    {
        switch (state)
        {
            case LiftState.Ready when isRecovering && timeScale < PausedBelow:
                return new LiftStep(LiftAction.Lift, LiftedScale, new LiftState.Lifted(timeScale, 0f));
            case LiftState.Lifted when timeScale != LiftedScale:
                return new LiftStep(LiftAction.LeaveSpeedAlone, timeScale, AfterLift(isRecovering));
            case LiftState.Lifted lifted:
                var elapsed = lifted.ScaledElapsed + scaledDelta;
                var isWaitOver = !isRecovering || elapsed >= HoldForScaledSeconds;

                return isWaitOver
                    ? new LiftStep(LiftAction.Restore, lifted.SavedScale, AfterLift(isRecovering))
                    : new LiftStep(LiftAction.None, timeScale, lifted with { ScaledElapsed = elapsed });
            case LiftState.Spent when !isRecovering:
                return new LiftStep(LiftAction.None, timeScale, new LiftState.Ready());
            default:
                return new LiftStep(LiftAction.None, timeScale, state);
        }
    }

    private static LiftState AfterLift(bool isRecovering) => isRecovering ? new LiftState.Spent() : new LiftState.Ready();
}
