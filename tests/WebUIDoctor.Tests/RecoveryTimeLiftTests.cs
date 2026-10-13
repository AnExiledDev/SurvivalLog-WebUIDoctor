using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class RecoveryTimeLiftTests
{
    private const float Paused = 0.0001f;
    private const float Frame = 1f / 60f;

    /// <summary>Runs frames at whatever scale the lift leaves, the way Unity hands out Time.deltaTime.</summary>
    private static (LiftState State, float Scale, int Frames) RunWhileRecovering(LiftState state, float scale, int maxFrames)
    {
        for (var frame = 1; frame <= maxFrames; frame++)
        {
            var step = RecoveryTimeLift.Tick(state, isRecovering: true, scale, Frame * scale);

            state = step.Next;

            if (step.Action is LiftAction.Lift or LiftAction.Restore) scale = step.Scale;

            if (step.Action == LiftAction.Restore) return (state, scale, frame);
        }

        return (state, scale, maxFrames);
    }

    [Fact]
    public void A_recovery_while_paused_gets_a_full_second_of_game_time_then_the_pause_comes_back()
    {
        // The stuck UI seen in play: paused game, recovery waiting a scaled second that took hours.
        var start = RecoveryTimeLift.Tick(LiftState.Initial, isRecovering: true, Paused, 0f);

        Assert.Equal(LiftAction.Lift, start.Action);
        Assert.True(start.Scale >= 1f);

        var gameTime = 0f;
        var state = start.Next;
        var scale = start.Scale;

        while (state is LiftState.Lifted)
        {
            var step = RecoveryTimeLift.Tick(state, isRecovering: true, scale, Frame * scale);

            if (step.Action == LiftAction.None) gameTime += Frame * scale;

            state = step.Next;

            if (step.Action == LiftAction.Restore) scale = step.Scale;
        }

        Assert.True(gameTime >= 1f, $"the game's one-second wait needs a second of game time, got {gameTime}");
        Assert.Equal(Paused, scale);
    }

    [Fact]
    public void A_recovery_at_normal_speed_is_left_alone()
    {
        var step = RecoveryTimeLift.Tick(LiftState.Initial, isRecovering: true, timeScale: 1f, Frame);

        Assert.Equal(LiftAction.None, step.Action);
    }

    [Fact]
    public void A_paused_game_with_no_recovery_is_never_touched()
    {
        var step = RecoveryTimeLift.Tick(LiftState.Initial, isRecovering: false, Paused, 0f);

        Assert.Equal(LiftAction.None, step.Action);
    }

    [Fact]
    public void A_speed_change_made_during_the_lift_is_kept()
    {
        var lifted = RecoveryTimeLift.Tick(LiftState.Initial, isRecovering: true, Paused, 0f).Next;
        var step = RecoveryTimeLift.Tick(lifted, isRecovering: true, timeScale: 4f, Frame * 4f);

        Assert.Equal(LiftAction.LeaveSpeedAlone, step.Action);

        var later = RecoveryTimeLift.Tick(step.Next, isRecovering: true, timeScale: 4f, Frame * 4f);

        Assert.Equal(LiftAction.None, later.Action);
    }

    [Fact]
    public void A_recovery_that_ends_early_puts_the_pause_back_at_once()
    {
        var lifted = RecoveryTimeLift.Tick(LiftState.Initial, isRecovering: true, Paused, 0f).Next;
        var step = RecoveryTimeLift.Tick(lifted, isRecovering: false, RecoveryTimeLift.LiftedScale, Frame);

        Assert.Equal(LiftAction.Restore, step.Action);
        Assert.Equal(Paused, step.Scale);
    }

    [Fact]
    public void One_recovery_is_lifted_once_and_the_next_recovery_is_lifted_again()
    {
        var lifted = RecoveryTimeLift.Tick(LiftState.Initial, isRecovering: true, Paused, 0f);
        var (state, scale, _) = RunWhileRecovering(lifted.Next, lifted.Scale, maxFrames: 600);

        Assert.Equal(Paused, scale);
        Assert.Equal(LiftAction.None, RecoveryTimeLift.Tick(state, isRecovering: true, Paused, 0f).Action);

        var ended = RecoveryTimeLift.Tick(state, isRecovering: false, Paused, 0f).Next;
        var nextRecovery = RecoveryTimeLift.Tick(ended, isRecovering: true, Paused, 0f);

        Assert.Equal(LiftAction.Lift, nextRecovery.Action);
    }
}
