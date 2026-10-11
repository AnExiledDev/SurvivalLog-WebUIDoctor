using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class AbandonRetryTests
{
    private static readonly DateTime T0 = new(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc);

    private static readonly RetrySettings Settings = new(
        SettleDelay: TimeSpan.FromSeconds(3),
        DirectInvokeAfter: TimeSpan.FromSeconds(5),
        RepairDelay: TimeSpan.FromSeconds(2),
        ForgiveAfter: TimeSpan.FromMinutes(2),
        Backoff: new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2) });

    private static readonly RecoveryFlags Calm = new(IsCrashed: false, NeedsRecover: false, IsRecovering: false, Attempts: 0, IsAbandoned: false);
    private static readonly RecoveryFlags Recovering = new(IsCrashed: true, NeedsRecover: false, IsRecovering: true, Attempts: 1, IsAbandoned: false);
    private static readonly RecoveryFlags Crashed = new(IsCrashed: true, NeedsRecover: false, IsRecovering: false, Attempts: 1, IsAbandoned: false);

    private static RetryState AfterFirstGiveUp() => AbandonRetry.OnGameAbandoned(RetryState.Initial, T0, Settings).Next;

    private static RetryState IdleAfter(int restartsSinceUiWasUp, DateTime calmSince) =>
        new RetryState.Idle(new RestartCount(restartsSinceUiWasUp, restartsSinceUiWasUp), calmSince);

    private static AbandonDecision GiveUpAt(RetryState state, DateTime now) => AbandonRetry.OnGameAbandoned(state, now, Settings).Decision;

    /// <summary>Seconds between a give-up and the moment the game is asked to recover again.</summary>
    private static double SecondsUntilRetry(RetryState state, DateTime now)
    {
        var next = Assert.IsType<RetryState.Settling>(AbandonRetry.OnGameAbandoned(state, now, Settings).Next);

        return (next.RequestAt - now).TotalSeconds;
    }

    /// <summary>A restart whose forced recovery ran and failed again, ready for the next give-up.</summary>
    private static RetryState FailedRestart(RetryState state, DateTime now) =>
        AbandonRetry.OnOurRecoveryStarted(AbandonRetry.OnGameAbandoned(state, now, Settings).Next, now, Settings);

    [Fact]
    public void The_first_give_up_restarts_the_browser_after_only_the_settle_delay()
    {
        // Every recovery in the logs that worked, worked on the first hard restart; making it wait costs play time.
        var (decision, next) = AbandonRetry.OnGameAbandoned(RetryState.Initial, T0, Settings);

        Assert.Equal(AbandonDecision.RestartBrowser, decision);
        Assert.Equal(3, SecondsUntilRetry(RetryState.Initial, T0));
        Assert.Equal(new RestartCount(1, 1), next.Restarts);
    }

    [Fact]
    public void Each_further_failed_restart_waits_longer_then_settles_on_every_two_minutes()
    {
        var state = FailedRestart(RetryState.Initial, T0);
        var waits = new List<double>();

        for (var round = 0; round < 5; round++)
        {
            waits.Add(SecondsUntilRetry(state, T0) - Settings.SettleDelay.TotalSeconds);
            state = FailedRestart(state, T0);
        }

        Assert.Equal(new double[] { 10, 30, 60, 120, 120 }, waits);
    }

    [Fact]
    public void Never_gives_up_on_a_ui_that_keeps_failing()
    {
        // The game's dialog blocks every click, so letting it show is never better for the player than retrying.
        var state = RetryState.Initial;

        for (var round = 0; round < 20; round++)
        {
            Assert.Equal(AbandonDecision.RestartBrowser, GiveUpAt(state, T0));
            state = FailedRestart(state, T0);
        }

        Assert.Equal(20, state.Restarts.ThisLaunch);
    }

    [Fact]
    public void A_give_up_caused_by_our_own_kill_is_suppressed_without_another_kill()
    {
        // 2026-10-10 19:48: killing Chromium made the game give up a second time and its dialog got through.
        var settling = AfterFirstGiveUp();

        var (decision, next) = AbandonRetry.OnGameAbandoned(settling, T0.AddSeconds(1), Settings);

        Assert.Equal(AbandonDecision.SuppressDuringRestart, decision);
        Assert.Equal(settling, next);
    }

    [Fact]
    public void A_give_up_while_waiting_for_the_game_to_start_recovering_is_also_suppressed()
    {
        var (_, awaiting) = AbandonRetry.Tick(AfterFirstGiveUp(), T0.AddSeconds(3), Crashed, Settings);

        var (decision, next) = AbandonRetry.OnGameAbandoned(awaiting, T0.AddSeconds(4), Settings);

        Assert.Equal(AbandonDecision.SuppressDuringRestart, decision);
        Assert.Equal(awaiting, next);
    }

    [Fact]
    public void A_forced_recovery_that_fails_again_gets_another_restart()
    {
        var running = AbandonRetry.OnOurRecoveryStarted(AfterFirstGiveUp(), T0.AddSeconds(4), Settings);

        var (decision, next) = AbandonRetry.OnGameAbandoned(running, T0.AddSeconds(40), Settings);

        Assert.Equal(AbandonDecision.RestartBrowser, decision);
        Assert.Equal(2, next.Restarts.SinceUiWasUp);
    }

    [Fact]
    public void Waits_for_chromium_to_settle_before_asking_the_game_to_recover()
    {
        var settling = AfterFirstGiveUp();

        Assert.Equal(RetryAction.None, AbandonRetry.Tick(settling, T0.AddSeconds(2), Crashed, Settings).Action);
        Assert.Equal(RetryAction.RequestGameRecovery, AbandonRetry.Tick(settling, T0.AddSeconds(3), Crashed, Settings).Action);
    }

    [Fact]
    public void Never_requests_recovery_while_the_game_is_already_recovering()
    {
        var settling = AfterFirstGiveUp();

        var (action, next) = AbandonRetry.Tick(settling, T0.AddSeconds(30), Recovering, Settings);

        Assert.Equal(RetryAction.None, action);
        Assert.Equal(settling, next);
    }

    [Fact]
    public void Calls_recovery_directly_even_while_the_games_own_attempt_runs()
    {
        // Every recovery in the log that brought a browser back had this call; the game's attempt alone timed out.
        var (_, awaiting) = AbandonRetry.Tick(AfterFirstGiveUp(), T0.AddSeconds(3), Crashed, Settings);

        Assert.Equal(RetryAction.None, AbandonRetry.Tick(awaiting, T0.AddSeconds(7), Recovering, Settings).Action);
        Assert.Equal(RetryAction.InvokeRecoveryDirectly, AbandonRetry.Tick(awaiting, T0.AddSeconds(8), Recovering, Settings).Action);
    }

    [Fact]
    public void No_second_direct_call_once_ours_has_started()
    {
        var (_, awaiting) = AbandonRetry.Tick(AfterFirstGiveUp(), T0.AddSeconds(3), Crashed, Settings);

        var running = AbandonRetry.OnOurRecoveryStarted(awaiting, T0.AddSeconds(4), Settings);

        Assert.NotEqual(RetryAction.InvokeRecoveryDirectly, AbandonRetry.Tick(running, T0.AddMinutes(5), Calm, Settings).Action);
    }

    [Fact]
    public void Closes_the_leftover_dialog_once_the_forced_recovery_has_finished()
    {
        var running = AbandonRetry.OnOurRecoveryStarted(AfterFirstGiveUp(), T0.AddSeconds(4), Settings);

        Assert.Equal(RetryAction.None, AbandonRetry.Tick(running, T0.AddSeconds(5), Calm, Settings).Action);
        Assert.Equal(RetryAction.None, AbandonRetry.Tick(running, T0.AddSeconds(30), Recovering, Settings).Action);

        var (action, next) = AbandonRetry.Tick(running, T0.AddSeconds(30), Calm, Settings);

        Assert.Equal(RetryAction.RepairUi, action);
        Assert.IsType<RetryState.Idle>(next);
    }

    [Fact]
    public void A_direct_recovery_call_is_followed_by_the_repair_too()
    {
        var (_, awaiting) = AbandonRetry.Tick(AfterFirstGiveUp(), T0.AddSeconds(3), Crashed, Settings);
        var (_, invoked) = AbandonRetry.Tick(awaiting, T0.AddSeconds(8), Crashed, Settings);

        Assert.Equal(RetryAction.RepairUi, AbandonRetry.Tick(invoked, T0.AddSeconds(20), Calm, Settings).Action);
    }

    [Fact]
    public void A_recovery_the_game_finishes_on_its_own_gets_the_same_ui_repair_as_ours()
    {
        // The game's own recoveries never reach the RecoverWebView hook, so the flags are the only way to see them.
        var (startAction, recovering) = AbandonRetry.Tick(RetryState.Initial, T0, Recovering, Settings);
        var (endAction, next) = AbandonRetry.Tick(recovering, T0.AddSeconds(20), Calm, Settings);

        Assert.Equal(RetryAction.None, startAction);
        Assert.IsType<RetryState.GameRecovering>(recovering);
        Assert.Equal(RetryAction.RepairUi, endAction);
        Assert.IsType<RetryState.Idle>(next);
    }

    [Fact]
    public void A_recovery_the_game_fails_on_its_own_is_left_to_the_give_up_handling()
    {
        var (_, recovering) = AbandonRetry.Tick(RetryState.Initial, T0, Recovering, Settings);

        var (action, next) = AbandonRetry.Tick(recovering, T0.AddSeconds(30), Crashed, Settings);

        Assert.Equal(RetryAction.None, action);
        Assert.IsType<RetryState.Idle>(next);
    }

    [Fact]
    public void A_give_up_during_the_games_own_recovery_restarts_the_browser()
    {
        // 2026-10-11 01:58:45: the hook fired at the end of a game-only recovery that timed out.
        var (_, recovering) = AbandonRetry.Tick(RetryState.Initial, T0, Recovering, Settings);

        Assert.Equal(AbandonDecision.RestartBrowser, GiveUpAt(recovering, T0.AddSeconds(36)));
    }

    [Fact]
    public void Two_calm_minutes_mean_the_next_interface_error_is_retried_without_waiting()
    {
        var failing = IdleAfter(3, T0);

        var (_, almost) = AbandonRetry.Tick(failing, T0.AddSeconds(119), Calm, Settings);
        var (_, upLongEnough) = AbandonRetry.Tick(failing, T0.AddMinutes(2), Calm, Settings);

        Assert.Equal(60 + 3, SecondsUntilRetry(almost, T0.AddSeconds(119)));
        Assert.Equal(3, SecondsUntilRetry(upLongEnough, T0.AddMinutes(2)));
    }

    [Fact]
    public void A_ui_that_is_still_crashed_is_not_calm_and_keeps_the_wait()
    {
        var failing = IdleAfter(3, T0);

        var (_, stillCrashed) = AbandonRetry.Tick(failing, T0.AddMinutes(5), Crashed, Settings);
        var (_, shortlyAfter) = AbandonRetry.Tick(stillCrashed, T0.AddMinutes(6), Calm, Settings);
        var (_, upLongEnough) = AbandonRetry.Tick(shortlyAfter, T0.AddMinutes(7), Calm, Settings);

        Assert.Equal(60 + 3, SecondsUntilRetry(shortlyAfter, T0.AddMinutes(6)));
        Assert.Equal(3, SecondsUntilRetry(upLongEnough, T0.AddMinutes(7)));
    }

    [Fact]
    public void The_wait_starts_over_but_the_launch_count_is_kept()
    {
        var (_, forgiven) = AbandonRetry.Tick(IdleAfter(3, T0), T0.AddMinutes(2), Calm, Settings);

        Assert.Equal(new RestartCount(0, 3), forgiven.Restarts);
    }
}
