namespace WebUIDoctor.Core;

/// <summary>
/// The game's UI recovery counts its waits in game time: one second before it restarts the browser, then up to 30
/// seconds for the new page (RecoverWebView and CreateAndLoadRootWebView, scaled UniTask.Delay). The game's pause is a
/// time scale of 0.0001, so while paused those waits take hours and the UI never comes back. This runs time at normal
/// speed while a recovery attempt is under way, then puts the pause back.
/// </summary>
public abstract record LiftState
{
    /// <summary>No lift active; a recovery seen while paused gets one.</summary>
    public sealed record Ready : LiftState;

    /// <summary>Time runs at normal speed for <paramref name="Attempt"/>; <paramref name="SavedScale"/> goes back afterwards.</summary>
    public sealed record Lifted(float SavedScale, int Attempt, float ScaledElapsed) : LiftState;

    /// <summary><paramref name="Attempt"/> used its whole lift; the game's next attempt gets a new one.</summary>
    public sealed record Spent(int Attempt) : LiftState;

    public static LiftState Initial => new Ready();
}

public enum LiftAction
{
    None,

    /// <summary>Set the time scale to <see cref="RecoveryTimeLift.LiftedScale"/>.</summary>
    Lift,

    /// <summary>Set the time scale back to the step's <c>Scale</c>.</summary>
    Restore,

    /// <summary>The player or the game changed the speed while lifted; leave its choice alone.</summary>
    LeaveSpeedAlone,
}

/// <param name="IsRecovering">The game's isRecovering flag.</param>
/// <param name="Attempt">The game's recoverAttempts; it goes up at the start of every attempt.</param>
/// <param name="IsSpeedPaused">The game's own speed setting says paused (GameTimeManager.IsPausedScale), which a lift does not change.</param>
/// <param name="TimeScale">Unity's time scale right now.</param>
/// <param name="ScaledDelta">Game time that passed this frame (Time.deltaTime), the clock the game's waits count.</param>
public readonly record struct LiftInput(bool IsRecovering, int Attempt, bool IsSpeedPaused, float TimeScale, float ScaledDelta);

public readonly record struct LiftStep(LiftAction Action, float Scale, LiftState Next);

public static class RecoveryTimeLift
{
    public const float LiftedScale = 1f;

    /// <summary>The game's paused scale is 0.0001; its slowest real speed is 0.3.</summary>
    public const float PausedBelow = 0.01f;

    /// <summary>
    /// The game's own waits in one attempt add up to 31 s of game time (1 s before the restart, 30 s page timeout), and
    /// the margin covers the frames around them. Past this the attempt is stuck on something time does not fix, so the
    /// pause goes back rather than letting a paused game run on.
    /// </summary>
    public const float MaxLiftPerAttempt = 40f;

    public static LiftStep Tick(LiftState state, LiftInput game)
    {
        switch (state)
        {
            case LiftState.Ready:
                return StartLift(game);
            case LiftState.Lifted when !game.IsSpeedPaused || game.TimeScale != LiftedScale:
                return new LiftStep(LiftAction.LeaveSpeedAlone, game.TimeScale, new LiftState.Ready());
            case LiftState.Lifted lifted when !game.IsRecovering:
                return new LiftStep(LiftAction.Restore, lifted.SavedScale, new LiftState.Ready());
            case LiftState.Lifted lifted when game.Attempt != lifted.Attempt:
                // The game can end one attempt and start the next in the same frame; the new attempt's waits start from zero.
                return new LiftStep(LiftAction.None, game.TimeScale, lifted with { Attempt = game.Attempt, ScaledElapsed = 0f });
            case LiftState.Lifted lifted:
                return KeepLifting(lifted, game);
            case LiftState.Spent when !game.IsRecovering:
                return new LiftStep(LiftAction.None, game.TimeScale, new LiftState.Ready());
            case LiftState.Spent spent when game.Attempt != spent.Attempt:
                return StartLift(game);
            default:
                return new LiftStep(LiftAction.None, game.TimeScale, state);
        }
    }

    private static LiftStep StartLift(LiftInput game)
    {
        var isStuckOnPause = game.IsRecovering && game.IsSpeedPaused && game.TimeScale < PausedBelow;

        return isStuckOnPause
            ? new LiftStep(LiftAction.Lift, LiftedScale, new LiftState.Lifted(game.TimeScale, game.Attempt, 0f))
            : new LiftStep(LiftAction.None, game.TimeScale, new LiftState.Ready());
    }

    private static LiftStep KeepLifting(LiftState.Lifted lifted, LiftInput game)
    {
        var elapsed = lifted.ScaledElapsed + game.ScaledDelta;

        return elapsed >= MaxLiftPerAttempt
            ? new LiftStep(LiftAction.Restore, lifted.SavedScale, new LiftState.Spent(lifted.Attempt))
            : new LiftStep(LiftAction.None, game.TimeScale, lifted with { ScaledElapsed = elapsed });
    }
}
