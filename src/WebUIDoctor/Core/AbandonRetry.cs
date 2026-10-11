using System;
using System.Collections.Generic;

namespace WebUIDoctor.Core;

/// <param name="Backoff">
/// How long to wait before the next hard restart while the UI keeps failing: the first entry after one failed
/// restart, the last entry repeated forever. There is no give-up: the game's error dialog blocks every click, so
/// stopping would leave the player no better off than trying again.
/// </param>
/// <param name="ForgiveAfter">How long the UI must stay up before the next interface error is retried without waiting.</param>
public sealed record RetrySettings(
    TimeSpan SettleDelay,
    TimeSpan DirectInvokeAfter,
    TimeSpan RepairDelay,
    TimeSpan ForgiveAfter,
    IReadOnlyList<TimeSpan> Backoff);

/// <summary>Hard restarts since the UI last stayed up, which sets the wait before the next one, and for the whole launch.</summary>
public readonly record struct RestartCount(int SinceUiWasUp, int ThisLaunch)
{
    public RestartCount Spend() => new(SinceUiWasUp + 1, ThisLaunch + 1);

    public RestartCount Forgiven() => this with { SinceUiWasUp = 0 };

    /// <summary>No wait for the first restart: every recovery in the logs that worked, worked on that one.</summary>
    public TimeSpan WaitBeforeNext(RetrySettings settings)
    {
        if (SinceUiWasUp == 0 || settings.Backoff.Count == 0) return TimeSpan.Zero;

        return settings.Backoff[Math.Min(SinceUiWasUp, settings.Backoff.Count) - 1];
    }
}

public enum AbandonDecision
{
    /// <summary>Kill Chromium, skip the dialog and make the game recover again once the wait is over.</summary>
    RestartBrowser,

    /// <summary>A give-up caused by our own kill while a restart is under way: skip the dialog, do nothing else.</summary>
    SuppressDuringRestart,
}

public enum RetryAction
{
    None,

    /// <summary>Reset the game's recovery bookkeeping and ask it to recover again.</summary>
    RequestGameRecovery,

    /// <summary>
    /// Call the game's recovery routine directly. Done even when the game has started its own attempt: every
    /// recovery that brought a browser back in the logs had this call, and the game's attempt alone timed out.
    /// </summary>
    InvokeRecoveryDirectly,

    /// <summary>A recovery has finished: close any error dialog left on screen, give clicks back and release a stuck pause.</summary>
    RepairUi,
}

/// <summary>
/// What happens after the game gives up recovering its UI browser: kill Chromium, wait out the backoff, hand the game
/// a fresh recovery attempt, then clean up what the give-up left on screen.
/// Pure: time and game state come in as arguments.
/// </summary>
public abstract record RetryState(RestartCount Restarts)
{
    /// <summary>Nothing under way. <paramref name="CalmSince"/> is when the UI was last seen crashed or recovering.</summary>
    public sealed record Idle(RestartCount Count, DateTime CalmSince) : RetryState(Count);

    /// <summary>The game is recovering on its own; once it succeeds the UI gets the same repair as after ours.</summary>
    public sealed record GameRecovering(RestartCount Count) : RetryState(Count);

    /// <summary>Chromium was killed; waiting for it to die and for the backoff to run out.</summary>
    public sealed record Settling(RestartCount Count, DateTime RequestAt) : RetryState(Count);

    /// <summary>Recovery was requested; waiting a moment before calling the game's recovery directly.</summary>
    public sealed record AwaitingGame(RestartCount Count, DateTime InvokeAt) : RetryState(Count);

    /// <summary>The forced recovery is running; once it ends the leftover error dialog gets closed.</summary>
    public sealed record AwaitingRecoveryEnd(RestartCount Count, DateTime RepairAt) : RetryState(Count);

    public static RetryState Initial => new Idle(default, DateTime.MinValue);
}

public static class AbandonRetry
{
    /// <summary>Called when the game is about to show "automatic recovery has failed".</summary>
    public static (AbandonDecision Decision, RetryState Next) OnGameAbandoned(RetryState state, DateTime now, RetrySettings settings)
    {
        var isRestartUnderWay = state is RetryState.Settling or RetryState.AwaitingGame;

        if (isRestartUnderWay) return (AbandonDecision.SuppressDuringRestart, state);

        var requestAt = now + settings.SettleDelay + state.Restarts.WaitBeforeNext(settings);

        return (AbandonDecision.RestartBrowser, new RetryState.Settling(state.Restarts.Spend(), requestAt));
    }

    public static (RetryAction Action, RetryState Next) Tick(RetryState state, DateTime now, RecoveryFlags game, RetrySettings settings)
    {
        switch (state)
        {
            case RetryState.Idle idle:
                return (RetryAction.None, WhileIdle(idle, now, game, settings));
            case RetryState.GameRecovering recovering when !game.IsRecovering:
                // Still crashed means the attempt failed; the give-up handling takes it from here.
                return game.IsCrashed
                    ? (RetryAction.None, new RetryState.Idle(recovering.Restarts, now))
                    : (RetryAction.RepairUi, new RetryState.Idle(recovering.Restarts, now));
            case RetryState.Settling settling when now >= settling.RequestAt && !game.IsRecovering:
                return (RetryAction.RequestGameRecovery, new RetryState.AwaitingGame(settling.Restarts, now + settings.DirectInvokeAfter));
            case RetryState.AwaitingGame awaiting when now >= awaiting.InvokeAt:
                return (RetryAction.InvokeRecoveryDirectly, new RetryState.AwaitingRecoveryEnd(awaiting.Restarts, now + settings.RepairDelay));
            case RetryState.AwaitingRecoveryEnd ending when now >= ending.RepairAt && !game.IsRecovering:
                return (RetryAction.RepairUi, new RetryState.Idle(ending.Restarts, now));
            default:
                return (RetryAction.None, state);
        }
    }

    /// <summary>
    /// Our own call to the game's recovery routine has started (the hook only fires for calls we make; the game's
    /// own calls are inlined). Request nothing more and clean up once it ends.
    /// </summary>
    public static RetryState OnOurRecoveryStarted(RetryState state, DateTime now, RetrySettings settings) =>
        new RetryState.AwaitingRecoveryEnd(state.Restarts, now + settings.RepairDelay);

    private static RetryState WhileIdle(RetryState.Idle idle, DateTime now, RecoveryFlags game, RetrySettings settings)
    {
        if (game.IsRecovering) return new RetryState.GameRecovering(idle.Restarts);

        if (game.IsCrashed) return idle.CalmSince == now ? idle : idle with { CalmSince = now };

        var hasStayedUp = idle.Restarts.SinceUiWasUp > 0 && now - idle.CalmSince >= settings.ForgiveAfter;

        return hasStayedUp ? new RetryState.Idle(idle.Restarts.Forgiven(), now) : idle;
    }
}
