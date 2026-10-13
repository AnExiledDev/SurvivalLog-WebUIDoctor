using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using UnityEngine;
using Vuplex.WebView.Internal;
using WebUIDoctor.Core;
using Object = UnityEngine.Object;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Tests the path the other UI mods use to put their additions on the page: the webview the game's layer hands
/// out, a JavaScript result callback, and a page postMessage. Diagnostic only: it changes nothing in the game.
/// All members run on Unity's main thread.
/// </summary>
internal sealed class PageProbe
{
    public const string MessagePrefix = "slmod|webuidoctor|";
    private const string ModMessagePrefix = "slmod|";
    private static readonly TimeSpan AnswerWait = TimeSpan.FromSeconds(5);

    /// <summary>Plugin GUID of each UI mod, and the global its page script sets on the game's Root page.</summary>
    private static readonly (string Guid, string PageGlobal)[] UiMods =
    {
        ("com.ivmakk.survivallog.projectcook", "__projectCook"),
        ("com.ivmakk.survivallog.bettertrade", "__bettertrade"),
        ("com.ivmakk.survivallog.itemtotals", "__itemtotals"),
    };

    private readonly Action<string> _log;
    private int _probeCount;
    private int? _baselineOtherWebViews;
    private PendingProbe? _pending;

    public PageProbe(Action<string> log) => _log = log;

    public bool IsRunning => _pending is not null;

    private sealed class PendingProbe(
        string id, string reason, DateTime dueAt, bool isLayerWebViewLive, int otherLiveWebViews, IReadOnlyList<string> loadedGlobals)
    {
        public string Id { get; } = id;
        public string Reason { get; } = reason;
        public DateTime DueAt { get; } = dueAt;
        public bool IsLayerWebViewLive { get; } = isLayerWebViewLive;
        public int OtherLiveWebViews { get; } = otherLiveWebViews;
        public IReadOnlyList<string> LoadedGlobals { get; } = loadedGlobals;
        public string? ScriptReply { get; set; }
        public string? MessageReply { get; set; }
        public int LayerUpdates { get; set; }
        public int ModMessages { get; set; }
    }

    public void CountLayerUpdate()
    {
        if (_pending is not null) _pending.LayerUpdates++;
    }

    /// <summary>BaseWebView.HandleMessageEmitted prefix. False means the message was ours and the game should not see it.</summary>
    public bool OnPageMessage(string? message)
    {
        if (message is null || !message.StartsWith(ModMessagePrefix, StringComparison.Ordinal)) return true;

        if (!message.StartsWith(MessagePrefix, StringComparison.Ordinal))
        {
            if (_pending is not null) _pending.ModMessages++;

            return true;
        }

        var reply = message[MessagePrefix.Length..];

        if (_pending is not null && reply.Contains($"\"id\":\"{_pending.Id}\"", StringComparison.Ordinal)) _pending.MessageReply = reply;
        else _log("page link probe: late page message " + reply);

        return false;
    }

    public void Start(WebUILayer layer, string reason, DateTime now)
    {
        if (_pending is not null)
        {
            _log($"page link probe ({reason}) skipped: probe {_pending.Id} is still waiting");

            return;
        }

        var id = $"p{++_probeCount}";
        var webView = layer.canvasWebViewPrefab?.WebView;
        var layerWebView = webView?.TryCast<BaseWebView>();
        var isLayerWebViewLive = layerWebView != null && layerWebView.IsInitialized && !layerWebView.IsDisposed;
        var otherLive = LogWebViews(id, reason, layerWebView);

        _baselineOtherWebViews ??= otherLive;
        _pending = new PendingProbe(
            id, reason, now + AnswerWait, isLayerWebViewLive, Math.Max(0, otherLive - _baselineOtherWebViews.Value), LoadedModGlobals());

        if (webView is null) return;

        var probe = _pending;

        try
        {
            webView.ExecuteJavaScript(ProbeScript(id), (Action<string>)(result => probe.ScriptReply = result ?? ""));
        }
        catch (Exception ex)
        {
            _log($"page link probe {id}: ExecuteJavaScript threw: {ex.Message}");
        }
    }

    public void FinishWhenDue(DateTime now)
    {
        if (_pending is not { } probe || now < probe.DueAt) return;

        _pending = null;

        var reply = probe.ScriptReply ?? probe.MessageReply;
        var result = new PageProbeResult(
            probe.IsLayerWebViewLive,
            probe.OtherLiveWebViews,
            probe.LayerUpdates,
            HasScriptReply: probe.ScriptReply is not null,
            HasMessageReply: probe.MessageReply is not null,
            PageLink.MissingModScripts(reply, probe.LoadedGlobals),
            PageLink.BlockedFrames(reply));
        var missing = result.MissingModScripts.Count == 0 ? "none" : string.Join(", ", result.MissingModScripts);
        var blocked = result.BlockedFrames.Count == 0 ? "none" : string.Join("; ", result.BlockedFrames);

        _log($"page link probe {probe.Id} ({probe.Reason}): script reply={Show(probe.ScriptReply)} page message={Show(probe.MessageReply)}");
        _log($"page link probe {probe.Id} ({probe.Reason}): layer updates={probe.LayerUpdates} mod page messages={probe.ModMessages} " +
             $"missing mod scripts={missing} blocked frames={blocked}");
        _log($"page link verdict {probe.Id} ({probe.Reason}): {Explain(PageLink.Decide(result), blocked)}");
    }

    private static string Explain(PageLinkVerdict verdict, string blocked) =>
        verdict == PageLinkVerdict.FramesBlocked
            ? $"{verdict} - broken: the page cannot read its frames, so mods that add to game windows fail. Blocked: {blocked}"
            : verdict.ToString();

    /// <summary>Logs every webview the game has, and returns how many live ones are not the layer's.</summary>
    private int LogWebViews(string id, string reason, BaseWebView? layerWebView)
    {
        var layerPointer = layerWebView?.Pointer ?? IntPtr.Zero;
        var otherLive = 0;

        _log($"page link probe {id} ({reason}): layer webview {Describe(layerWebView)}");

        foreach (var found in Object.FindObjectsByType(Il2CppType.Of<BaseWebView>(), FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var other = found.TryCast<BaseWebView>();

            if (other == null || other.Pointer == layerPointer) continue;

            if (other.IsInitialized && !other.IsDisposed) otherLive++;

            _log($"page link probe {id}: other webview {Describe(other)}");
        }

        return otherLive;
    }

    private static string Describe(BaseWebView? webView)
    {
        if (webView == null) return "none";

        return $"0x{webView.Pointer.ToInt64():X} '{webView.gameObject.name}' initialized={webView.IsInitialized} disposed={webView.IsDisposed} " +
               $"pendingScriptCallbacks={webView._pendingJavaScriptResultCallbacks?.Count.ToString() ?? "null"} url={webView.Url}";
    }

    private static IReadOnlyList<string> LoadedModGlobals()
    {
        var plugins = IL2CPPChainloader.Instance.Plugins;

        return UiMods.Where(mod => plugins.ContainsKey(mod.Guid)).Select(mod => mod.PageGlobal).ToList();
    }

    /// <summary>
    /// Answers on both channels with the page's state; each mod's global is checked whether or not that mod is loaded.
    /// Every frame's document is read the way the page mods read it, and each read that throws is listed under "blocked".
    /// </summary>
    private static string ProbeScript(string id)
    {
        var globals = string.Join(",", UiMods.Select(mod => $"'{mod.PageGlobal}'"));

        return "(function(){try{" +
               $"var r={{id:'{id}',url:location.href,ready:document.readyState,mods:{{}},frames:[],blocked:[]}};" +
               $"[{globals}].forEach(function(n){{r.mods[n]=!!window[n];}});" +
               "var f=document.getElementsByTagName('iframe');" +
               "for(var i=0;i<f.length;i++){var n=(f[i].getAttribute('src')||'').split('?')[0].split('/').pop();r.frames.push(n);" +
               "try{f[i].contentWindow.document.readyState;}" +
               "catch(e){r.blocked.push({frame:n,error:((e&&e.name)||'Error')+': '+((e&&e.message)||String(e))});}}" +
               "var j=JSON.stringify(r);" +
               $"try{{window.vuplex.postMessage('{MessagePrefix}'+j);}}catch(e){{r.post=String(e);j=JSON.stringify(r);}}" +
               "return j;" +
               "}catch(e){return 'probe error: '+e;}})()";
    }

    private static string Show(string? reply) => reply is null ? "(none)" : reply.Length <= 400 ? reply : reply[..400] + "...";
}
