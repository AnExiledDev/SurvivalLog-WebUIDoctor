using System;
using System.Collections.Generic;
using GameCore.HotUpdate.ReduxUI;
using WebUIDoctor.Adapters;
using WebUIDoctor.Core;

namespace WebUIDoctor;

/// <summary>
/// Turns the game's "automatic recovery has failed" into one more recovery attempt on a fresh Chromium,
/// then closes whatever error dialog the give-up left on screen.
/// All members run on Unity's main thread (Harmony patches on WebUILayer and the ticker's Update).
/// </summary>
internal sealed class Doctor
{
    private static readonly TimeSpan AbandonPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RepairSettleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BaselineProbeDelay = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan ProbeAfterRepairDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ClockCheckAfterRepairDelay = TimeSpan.FromSeconds(40);
    private const int MaxReportedFaults = 20;

    private readonly DoctorLog _log;
    private readonly RetrySettings _settings;
    private readonly bool _isHardRestartEnabled;
    private readonly int _gamePid;
    private readonly RepairHotkey _repairHotkey;
    private readonly RepairHotkey _probeHotkey;
    private readonly PageProbe _probe;
    private readonly ClockWatch _clock;
    private readonly ClickWatch _clicks;
    private RetryState _state = RetryState.Initial;
    private WebUILayer? _layer;
    private bool _hasLoggedSettings;
    private bool _wasAbandonFlagSet;
    private bool _wasAbandonSeenByPatch;
    private DateTime _nextAbandonPoll = DateTime.MinValue;
    private RecoveryFlags? _lastFlags;
    private (DateTime DueAt, RepairTrigger Trigger)? _pendingRepairFinish;
    private DateTime? _baselineProbeAt;
    private DateTime? _probeAfterRepairAt;
    private DateTime? _flagCheckAt;
    private bool _isFlagRestartSpent;
    private RecoveryFlags _flags;
    private readonly HashSet<string> _reportedFaults = new();
    private readonly (string Name, Action<WebUILayer, DateTime> Run)[] _steps;

    public Doctor(DoctorLog log, RetrySettings settings, bool isHardRestartEnabled, int gamePid, RepairHotkey repairHotkey, RepairHotkey probeHotkey, PageProbe probe, ClockWatch clock, ClickWatch clicks)
    {
        _log = log;
        _settings = settings;
        _isHardRestartEnabled = isHardRestartEnabled;
        _gamePid = gamePid;
        _repairHotkey = repairHotkey;
        _probeHotkey = probeHotkey;
        _probe = probe;
        _clock = clock;
        _clicks = clicks;

        // Retry first: it is the step that gets the UI back, so a failure in a diagnostic step must not starve it.
        _steps = new (string, Action<WebUILayer, DateTime>)[]
        {
            ("recovery retry", AdvanceRetry),
            ("flag watch", WatchFlags),
            ("repair hotkey", RepairOnHotkey),
            ("repair finish", FinishRepairWhenDue),
            ("browser flag check", CheckBrowserFlagsWhenDue),
            ("page probe", ProbePageLink),
            ("clock watch", (layer, now) => _clock.Tick(layer, now)),
            ("click second look", (_, now) => _clicks.Tick(now)),
        };
    }

    public void SeeLayer(WebUILayer layer)
    {
        _layer = layer;

        if (_hasLoggedSettings) return;

        _hasLoggedSettings = true;
        _baselineProbeAt = DateTime.UtcNow + BaselineProbeDelay;
        LogGameRecoverySettings();
    }

    /// <summary>NotifyRecoverAbandoned prefix. True means the game's error dialog should be skipped.</summary>
    public bool InterceptAbandon(WebUILayer layer)
    {
        SeeLayer(layer);

        var shouldSkipDialog = HandleAbandon("NotifyRecoverAbandoned hook", canSkipDialog: true);

        // Only a give-up the game went on to show sets its flag. A skipped one would leave this set and swallow
        // the next give-up the flag poll has to catch.
        _wasAbandonSeenByPatch = !shouldSkipDialog;

        return shouldSkipDialog;
    }

    /// <summary>
    /// RecoverWebView prefix. Fires only for our own direct call: the game's own recoveries never reach it
    /// (every "game recovery starting" line in the log follows "calling RecoverWebView directly").
    /// </summary>
    public void OnOurRecoveryStarted(WebUILayer layer)
    {
        SeeLayer(layer);
        _log.Write($"game recovery starting: attempts={layer.recoverAttempts} crashed={layer.isBrowserCrashed} needsRecover={layer.needsRecover}");
        _state = AbandonRetry.OnOurRecoveryStarted(_state, DateTime.UtcNow, _settings);
    }

    /// <summary>
    /// StartChromiumPipeWatchdog prefix. The game calls it right after TerminateBrowserProcess and right before it
    /// creates the UI webview, at startup, on an init retry and in every recovery, so Vuplex accepts launch arguments
    /// here. A recovery's relaunch otherwise comes up without the page-access flags.
    /// </summary>
    public void OnBrowserAboutToStart(WebUILayer layer) =>
        BrowserFlags.HandOver(ChromiumProcess.LaunchArguments(layer.noSandboxEnabled), _log.Write);

    public void Tick()
    {
        var layer = _layer;

        if (layer == null || layer.WasCollected) return;

        var now = DateTime.UtcNow;

        if (!TryReadFlags(layer)) return;

        foreach (var (name, run) in _steps)
        {
            try
            {
                run(layer, now);
            }
            catch (Exception ex)
            {
                ReportFault(name, ex);
            }
        }
    }

    private bool TryReadFlags(WebUILayer layer)
    {
        try
        {
            _flags = new RecoveryFlags(layer.isBrowserCrashed, layer.needsRecover, layer.isRecovering, layer.recoverAttempts, layer.recoverAbandonNotified);

            return true;
        }
        catch (Exception ex)
        {
            ReportFault("flag read", ex);

            return false;
        }
    }

    /// <summary>Logs each distinct failure once, up to a cap: a broken step would otherwise log every frame.</summary>
    private void ReportFault(string step, Exception ex)
    {
        if (_reportedFaults.Count >= MaxReportedFaults || !_reportedFaults.Add(step + "|" + ex.Message)) return;

        _log.Write($"{step} failed (each distinct failure is logged once): {ex}");
    }

    private void RepairOnHotkey(WebUILayer layer, DateTime now)
    {
        if (_repairHotkey.WasPressed()) StartRepair(layer, RepairTrigger.Manual, now);
    }

    private bool HandleAbandon(string source, bool canSkipDialog)
    {
        if (!_isHardRestartEnabled)
        {
            _log.Write($"game gave up recovering the UI ({source}); hard restart is disabled in the config. The error dialog will show.");

            return false;
        }

        var now = DateTime.UtcNow;
        var (decision, next) = AbandonRetry.OnGameAbandoned(_state, now, _settings);

        _state = next;

        switch (decision)
        {
            case AbandonDecision.SuppressDuringRestart:
                _log.Write($"game gave up again ({source}) because of our own kill; restart already under way" + (canSkipDialog ? ", dialog skipped" : ""));
                return true;
            case AbandonDecision.RestartBrowser:
                var killed = WindowsProcesses.KillChromiumUnder(_gamePid, _log.Write);
                var retryIn = next is RetryState.Settling settling ? settling.RequestAt - now : _settings.SettleDelay;

                _log.Write($"game gave up recovering the UI ({source}); killed Chromium pids [{string.Join(", ", killed)}]; " +
                           $"retrying recovery in {retryIn.TotalSeconds:0}s (hard restart {next.Restarts.SinceUiWasUp} since the UI was last up, " +
                           $"{next.Restarts.ThisLaunch} this launch)");
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision, null);
        }
    }

    /// <summary>
    /// Fallback for when the NotifyRecoverAbandoned hook never fires (a small private method can be inlined
    /// away from Harmony). Watches the game's own flag instead; the dialog may already be showing then,
    /// and the repair after recovery closes it.
    /// </summary>
    private void PollAbandonFlag(DateTime now)
    {
        if (now < _nextAbandonPoll) return;

        _nextAbandonPoll = now + AbandonPollInterval;

        var isAbandonFlagSet = _flags.IsAbandoned;
        var isRisingEdge = isAbandonFlagSet && !_wasAbandonFlagSet;

        _wasAbandonFlagSet = isAbandonFlagSet;

        if (!isRisingEdge) return;

        if (_wasAbandonSeenByPatch)
        {
            _wasAbandonSeenByPatch = false;

            return;
        }

        _log.Write("game set recoverAbandonNotified without the NotifyRecoverAbandoned hook firing");
        HandleAbandon("flag poll", canSkipDialog: false);
    }

    private void AdvanceRetry(WebUILayer layer, DateTime now)
    {
        var (action, next) = AbandonRetry.Tick(_state, now, _flags, _settings);

        LogRetryTransition(_state, next);
        _state = next;

        if (_state is RetryState.Idle or RetryState.GameRecovering) PollAbandonFlag(now);

        switch (action)
        {
            case RetryAction.None:
                return;
            case RetryAction.RequestGameRecovery:
                RequestGameRecovery(layer);
                return;
            case RetryAction.InvokeRecoveryDirectly:
                _log.Write("game did not start recovery on its own; calling RecoverWebView directly");
                layer.RecoverWebView();
                return;
            case RetryAction.RepairUi:
                StartRepair(layer, RepairTrigger.AfterRecovery, now);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    /// <summary>The game's close action lands a frame or more later, so the rest of the repair waits for it.</summary>
    private void StartRepair(WebUILayer layer, RepairTrigger trigger, DateTime now)
    {
        UiRepair.Start(layer, trigger, _log.Write);
        _pendingRepairFinish = (now + RepairSettleDelay, trigger);
    }

    private void FinishRepairWhenDue(WebUILayer layer, DateTime now)
    {
        if (_pendingRepairFinish is not { } pending || now < pending.DueAt) return;

        _pendingRepairFinish = null;
        UiRepair.Finish(layer, pending.Trigger, _clicks, now, _log.Write);
        _probeAfterRepairAt = now + ProbeAfterRepairDelay;
        _clock.CheckAt(now + ClockCheckAfterRepairDelay);

        if (pending.Trigger == RepairTrigger.AfterRecovery) _flagCheckAt = now;
    }

    /// <summary>
    /// Checks whether the other UI mods can still reach the page: once after startup as a baseline, after every
    /// repair, and on Ctrl+F11 (pressed after opening a window those mods add to).
    /// </summary>
    private void ProbePageLink(WebUILayer layer, DateTime now)
    {
        _probe.FinishWhenDue(now);

        if (_probeHotkey.WasPressed()) _probe.Start(layer, "Ctrl+F11", now);

        if (_probe.IsRunning) return;

        if (now >= _probeAfterRepairAt)
        {
            _probeAfterRepairAt = null;
            _probe.Start(layer, "after repair", now);
        }
        else if (now >= _baselineProbeAt)
        {
            _baselineProbeAt = null;
            _probe.Start(layer, "baseline", now);
        }
    }

    private void RequestGameRecovery(WebUILayer layer)
    {
        layer.recoverAttempts = 0;
        layer.recoverAbandonNotified = false;
        layer.isBrowserCrashed = true;
        layer.needsRecover = true;
        _wasAbandonFlagSet = false;
        _wasAbandonSeenByPatch = false;
        _log.Write("reset the game's recovery bookkeeping and asked it to recover again");
    }

    private void LogRetryTransition(RetryState from, RetryState to)
    {
        if (from is RetryState.Idle && to is RetryState.GameRecovering)
            _log.Write("game is recovering the UI on its own; the UI gets repaired if it succeeds");
        else if (from is RetryState.Idle { Restarts.SinceUiWasUp: > 0 } && to is RetryState.Idle { Restarts.SinceUiWasUp: 0 })
            _log.Write($"UI stayed up for {_settings.ForgiveAfter.TotalMinutes:0} min; the next interface error is retried without waiting");
    }

    /// <summary>Logs every change to the game's recovery flags, to show how its retry counter runs out.</summary>
    private void WatchFlags(WebUILayer layer, DateTime now)
    {
        var flags = _flags;

        if (flags == _lastFlags) return;

        _lastFlags = flags;
        _log.Write("game recovery flags: " + flags);
    }

    /// <summary>
    /// A moment after each recovery, reads the relaunched browser's command line. Without the page-access flags every
    /// page mod is cut off, so the browser gets one restart through the game's own recovery, which passes the
    /// StartChromiumPipeWatchdog hook that hands the flags over.
    /// </summary>
    private void CheckBrowserFlagsWhenDue(WebUILayer layer, DateTime now)
    {
        if (_flagCheckAt is not { } dueAt || now < dueAt) return;

        _flagCheckAt = null;

        var isRecoveryUnderWay = _flags.IsCrashed || _flags.IsRecovering || _state is not RetryState.Idle;
        var (outcome, isRestartSpent) = FlagRestart.Decide(WindowsProcesses.ChromiumCommandLinesUnder(_gamePid), isRecoveryUnderWay, _isFlagRestartSpent);

        _isFlagRestartSpent = isRestartSpent;

        switch (outcome)
        {
            case FlagCheckOutcome.FlagsPresent:
                _log.Write("flag check after recovery: the browser has its page-access flags");
                return;
            case FlagCheckOutcome.BrowserNotFound:
                _log.Write("flag check after recovery: no browser process with a readable command line; nothing done");
                return;
            case FlagCheckOutcome.RecoveryUnderWay:
                _log.Write("flag check after recovery skipped: another recovery is under way and gets its own check");
                return;
            case FlagCheckOutcome.GiveUp:
                _log.Write("flag check after recovery: the browser is still missing its page-access flags after the one restart; " +
                           "not restarting again. UI mods that add to the game's windows may not work until the game is restarted");
                return;
            case FlagCheckOutcome.RestartBrowser:
                _log.Write("flag check after recovery: the browser is missing its page-access flags; restarting it once through the game's recovery");
                _probeAfterRepairAt = null;
                layer.RecoverWebView();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }
    }

    private void LogGameRecoverySettings()
    {
        try
        {
            _log.Write($"game recovery settings: MAX_RECOVER_ATTEMPTS={WebUILayer.MAX_RECOVER_ATTEMPTS} RECOVER_INIT_TIMEOUT_MS={WebUILayer.RECOVER_INIT_TIMEOUT_MS} RECOVER_TERMINATE_TIMEOUT_MS={WebUILayer.RECOVER_TERMINATE_TIMEOUT_MS}");
        }
        catch (Exception ex)
        {
            _log.Write("could not read the game's recovery settings: " + ex.Message);
        }
    }
}
