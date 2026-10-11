using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Compares the clock the CoreUI1 page draws with the game's time once a minute, and puts it right when they
/// differ: first through the game's own resync, then by posting the time into the page. The same read logs what
/// the page holds for its trap list, since that list lives in the same frame.
/// All members run on Unity's main thread; the script reply is only stored by its callback.
/// </summary>
internal sealed class ClockWatch
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RecheckDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AnswerWait = TimeSpan.FromSeconds(5);

    /// <summary>Finds the CoreUI1 frame from the Root page the way the trap list mod does.</summary>
    private const string FindFrame =
        "var fs=document.querySelectorAll('iframe'),w=null;" +
        "for(var i=0;i<fs.length;i++){var c=fs[i].contentWindow;if(!c)continue;" +
        "try{if(!/CoreUI1\\.html/i.test(String(c.location)))continue;}catch(e){continue;}w=c;break;}" +
        "if(!w)return 'no CoreUI1 frame';";

    private const string ReadScript =
        "(function(){try{" + FindFrame +
        "var s=w.eval('state');" +
        "var t=String(s.clockHour).padStart(2,'0')+':'+String(Math.floor(s.clockMinute/10)*10).padStart(2,'0');" +
        "var trap='active='+(s.trapsActive?1:0)+' traps='+(s.traps?s.traps.length:0)+' panel='+(w.document.querySelector('.trap-panel')?1:0);" +
        "return 'ok|'+t+'|'+trap;" +
        "}catch(e){return 'error: '+e;}})()";

    private readonly Action<string> _log;
    private ClockStep _step = ClockStep.Watching;
    private DateTime _nextCheckAt = DateTime.MinValue;
    private PendingRead? _pending;
    private ClockVerdict? _lastVerdict;
    private string _lastTrapDetail = "";

    public ClockWatch(Action<string> log) => _log = log;

    private sealed class PendingRead(DateTime dueAt, int gameSecondOfDay)
    {
        public DateTime DueAt { get; } = dueAt;
        public int GameSecondOfDay { get; } = gameSecondOfDay;
        public string? Reply { get; set; }
    }

    /// <summary>A recovered page should have had the game's 30-second resync by then.</summary>
    public void CheckAt(DateTime at)
    {
        if (at < _nextCheckAt) _nextCheckAt = at;
    }

    public void Tick(WebUILayer layer, DateTime now)
    {
        if (_pending is not null)
        {
            FinishWhenAnswered(layer, now);

            return;
        }

        if (now >= _nextCheckAt) StartRead(layer, now);
    }

    private void StartRead(WebUILayer layer, DateTime now)
    {
        _nextCheckAt = now + CheckInterval;

        var webView = layer.canvasWebViewPrefab?.WebView;

        if (webView is null || GameTime() is not { } time) return;

        var read = new PendingRead(now + AnswerWait, ClockDrift.SecondOfDay(time.GetTotalSeconds()));
        _pending = read;

        try
        {
            webView.ExecuteJavaScript(ReadScript, (Action<string>)(result => read.Reply = result ?? ""));
        }
        catch (Exception ex)
        {
            _pending = null;
            _log("clock watch: ExecuteJavaScript threw: " + ex.Message);
        }
    }

    private void FinishWhenAnswered(WebUILayer layer, DateTime now)
    {
        var read = _pending!;

        if (read.Reply is null && now < read.DueAt) return;

        _pending = null;

        var reading = ClockDrift.Read(read.Reply, read.GameSecondOfDay);
        var (action, next) = ClockHeal.Decide(_step, reading.Verdict);
        var gameTime = $"{read.GameSecondOfDay / 3600:00}:{read.GameSecondOfDay / 60 % 60:00}";

        LogReading(reading, gameTime, read.Reply);
        _step = next;
        Act(action, reading, gameTime, layer, now);
    }

    private void LogReading(ClockReading reading, string gameTime, string? reply)
    {
        if (reading.Verdict != _lastVerdict)
        {
            _lastVerdict = reading.Verdict;
            _log(reading.Verdict == ClockVerdict.Unreadable
                ? $"clock watch: page clock unreadable ({reply ?? "no answer"})"
                : $"clock watch: page clock {reading.PageTime}, game {gameTime}, drift {reading.DriftMinutes:+0;-0;0} min -> {reading.Verdict}");
        }

        if (reading.TrapDetail.Length == 0 || reading.TrapDetail == _lastTrapDetail) return;

        _lastTrapDetail = reading.TrapDetail;
        _log("clock watch: page trap list " + reading.TrapDetail);
    }

    private void Act(ClockAction action, ClockReading reading, string gameTime, WebUILayer layer, DateTime now)
    {
        switch (action)
        {
            case ClockAction.None:
                return;
            case ClockAction.AskGameToResync:
                _log($"clock watch: page clock {reading.PageTime} is {reading.DriftMinutes:+0;-0} game minutes off the game's {gameTime}; asking the game to resync it");
                GameTime()?.OnRequestTimeSync();
                break;
            case ClockAction.PostSyncDirectly:
                _log("clock watch: the game's resync did not reach the page; posting the time into the clock frame directly");
                PostTime(layer);
                break;
            case ClockAction.ReportGameResyncFixedIt:
                _log("clock watch: the game's own resync put the page clock right");
                return;
            case ClockAction.ReportDirectSyncFixedIt:
                _log("clock watch: the direct post put the page clock right; the game's link to the page clock is broken this launch");
                return;
            case ClockAction.ReportStillDrifted:
                _log("clock watch: still drifted after both fixes; will try again next check");
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }

        _lastVerdict = null;
        _nextCheckAt = now + RecheckDelay;
    }

    /// <summary>The same message the game sends; maxSeconds is left out so the page keeps its day length.</summary>
    private void PostTime(WebUILayer layer)
    {
        var webView = layer.canvasWebViewPrefab?.WebView;

        if (webView is null || GameTime() is not { } time) return;

        var seconds = ClockDrift.SecondOfDay(time.GetTotalSeconds());
        var script = "(function(){try{" + FindFrame +
                     $"w.postMessage({{type:'WebUI_CoreUI1_ClockSyncMsg',data:{{clockTotalSeconds:{seconds}}}}},'*');return 'posted';" +
                     "}catch(e){return 'error: '+e;}})()";

        webView.ExecuteJavaScript(script, (Action<string>)(result =>
        {
            if (result != "posted") _log("clock watch: direct post failed: " + result);
        }));
    }

    private static GameTimeManager? GameTime() => BaseSingleton<BattleLogicWorld>.Instance?._GameTimeManager;
}
