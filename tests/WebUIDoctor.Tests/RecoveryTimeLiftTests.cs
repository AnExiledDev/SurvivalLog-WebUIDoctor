using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class RecoveryTimeLiftTests
{
    private const float Paused = 0.0001f;
    private const float Frame = 1f / 60f;

    private static LiftInput Recovering(int attempt, float scale, bool isSpeedPaused = true) =>
        new(IsRecovering: true, attempt, isSpeedPaused, scale, Frame * scale);

    private static LiftInput NotRecovering(float scale, bool isSpeedPaused = true) =>
        new(IsRecovering: false, Attempt: 0, isSpeedPaused, scale, Frame * scale);

    private static LiftStep LiftFor(int attempt) => RecoveryTimeLift.Tick(LiftState.Initial, Recovering(attempt, Paused));

    /// <summary>
    /// Feeds frames of one attempt at whatever scale the lift leaves, the way Unity hands out Time.deltaTime. Stops at a
    /// restore, since a paused game would need hours of frames to reach the target.
    /// </summary>
    private static (LiftState State, float Scale) RunAttempt(LiftState state, float scale, int attempt, float gameSeconds)
    {
        var elapsed = 0f;

        while (elapsed < gameSeconds)
        {
            var step = RecoveryTimeLift.Tick(state, Recovering(attempt, scale));

            elapsed += Frame * scale;
            state = step.Next;

            if (step.Action is LiftAction.Lift or LiftAction.Restore) scale = step.Scale;

            if (step.Action == LiftAction.Restore) break;
        }

        return (state, scale);
    }

    [Fact]
    public void A_recovery_while_paused_runs_at_normal_speed_through_the_games_waits()
    {
        // The stuck UI seen in play: paused game, recovery waiting a second of game time that took hours.
        var start = LiftFor(attempt: 1);

        Assert.Equal(LiftAction.Lift, start.Action);
        Assert.Equal(1f, start.Scale);

        // One second before the browser restart, then up to 30 seconds for the new page.
        var (state, scale) = RunAttempt(start.Next, start.Scale, attempt: 1, gameSeconds: 31.5f);

        Assert.IsType<LiftState.Lifted>(state);
        Assert.Equal(1f, scale);
    }

    [Fact]
    public void The_pause_comes_back_as_soon_as_the_recovery_finishes()
    {
        var lifted = RunAttempt(LiftFor(attempt: 1).Next, 1f, attempt: 1, gameSeconds: 2f).State;
        var step = RecoveryTimeLift.Tick(lifted, NotRecovering(1f));

        Assert.Equal(LiftAction.Restore, step.Action);
        Assert.Equal(Paused, step.Scale);
    }

    [Fact]
    public void A_second_attempt_started_in_the_same_frame_still_gets_normal_speed()
    {
        // The game can fail attempt 1 and start attempt 2 inside one update, so isRecovering never reads false.
        var (afterFirst, _) = RunAttempt(LiftFor(attempt: 1).Next, 1f, attempt: 1, gameSeconds: 20f);
        var (afterSecond, scale) = RunAttempt(afterFirst, 1f, attempt: 2, gameSeconds: 31.5f);

        Assert.IsType<LiftState.Lifted>(afterSecond);
        Assert.Equal(1f, scale);
    }

    [Fact]
    public void An_attempt_stuck_past_its_waits_gets_the_pause_back_and_the_next_attempt_is_lifted_again()
    {
        var (state, scale) = RunAttempt(LiftFor(attempt: 1).Next, 1f, attempt: 1, gameSeconds: RecoveryTimeLift.MaxLiftPerAttempt + 1f);

        Assert.Equal(Paused, scale);
        Assert.Equal(LiftAction.None, RecoveryTimeLift.Tick(state, Recovering(1, Paused)).Action);
        Assert.Equal(LiftAction.Lift, RecoveryTimeLift.Tick(state, Recovering(2, Paused)).Action);
    }

    [Fact]
    public void A_recovery_at_normal_speed_is_left_alone()
    {
        var step = RecoveryTimeLift.Tick(LiftState.Initial, Recovering(1, 1f, isSpeedPaused: false));

        Assert.Equal(LiftAction.None, step.Action);
    }

    [Fact]
    public void A_paused_game_with_no_recovery_is_never_touched()
    {
        var step = RecoveryTimeLift.Tick(LiftState.Initial, NotRecovering(Paused));

        Assert.Equal(LiftAction.None, step.Action);
    }

    [Fact]
    public void The_player_choosing_normal_speed_during_the_lift_is_kept()
    {
        // 1x leaves Unity's scale at 1, the same as the lift; only the game's own speed setting shows the change.
        var step = RecoveryTimeLift.Tick(LiftFor(attempt: 1).Next, Recovering(1, 1f, isSpeedPaused: false));

        Assert.Equal(LiftAction.LeaveSpeedAlone, step.Action);
        Assert.Equal(LiftAction.None, RecoveryTimeLift.Tick(step.Next, NotRecovering(1f, isSpeedPaused: false)).Action);
    }

    [Fact]
    public void A_faster_speed_chosen_during_the_lift_is_kept()
    {
        var step = RecoveryTimeLift.Tick(LiftFor(attempt: 1).Next, Recovering(1, 4f));

        Assert.Equal(LiftAction.LeaveSpeedAlone, step.Action);
        Assert.Equal(LiftAction.None, RecoveryTimeLift.Tick(step.Next, Recovering(1, 4f)).Action);
    }

    [Fact]
    public void Pausing_again_during_the_recovery_gets_another_lift()
    {
        var afterPlayerResumed = RecoveryTimeLift.Tick(LiftFor(attempt: 1).Next, Recovering(1, 1f, isSpeedPaused: false)).Next;

        Assert.Equal(LiftAction.Lift, RecoveryTimeLift.Tick(afterPlayerResumed, Recovering(1, Paused)).Action);
    }

    [Fact]
    public void The_next_recovery_after_a_finished_one_is_lifted_again()
    {
        var finished = RecoveryTimeLift.Tick(LiftFor(attempt: 1).Next, NotRecovering(1f)).Next;

        Assert.Equal(LiftAction.Lift, RecoveryTimeLift.Tick(finished, Recovering(1, Paused)).Action);
    }
}
