using System;
using System.Collections.Generic;
using System.Linq;

namespace WebUIDoctor.Core;

/// <summary>
/// What one probe of the web UI saw. The other UI mods (Project Cook, Better Trade, Item Totals) push their
/// page scripts from the game's WebUILayer.OnUpdate through the layer's webview, and learn that a page needs
/// the script again from a reply: a JavaScript result callback (Project Cook) or a window.vuplex.postMessage
/// (Better Trade, Item Totals). A probe tests each link in that chain.
/// </summary>
/// <param name="IsLayerWebViewLive">The webview the layer hands out is initialized and not disposed.</param>
/// <param name="OtherLiveWebViews">Live webviews that are not the one the layer hands out.</param>
/// <param name="LayerUpdatesDuringProbe">WebUILayer.OnUpdate calls while the probe waited for answers; the mods tick there.</param>
/// <param name="HasScriptReply">The ExecuteJavaScript result callback answered.</param>
/// <param name="HasMessageReply">The page's postMessage reached BaseWebView.HandleMessageEmitted.</param>
/// <param name="MissingModScripts">Loaded mods whose page script is not on the page.</param>
public readonly record struct PageProbeResult(
    bool IsLayerWebViewLive,
    int OtherLiveWebViews,
    int LayerUpdatesDuringProbe,
    bool HasScriptReply,
    bool HasMessageReply,
    IReadOnlyList<string> MissingModScripts);

public enum PageLinkVerdict
{
    /// <summary>Every link works and every loaded mod's script is on the page.</summary>
    Healthy,

    /// <summary>The layer's webview is gone or not initialized, so nothing the mods send can land.</summary>
    WebViewNotLive,

    /// <summary>Another live webview exists beside the one the layer hands out; the mods may be talking to the wrong one.</summary>
    StaleWebViewReference,

    /// <summary>WebUILayer.OnUpdate stopped running, so the mods never tick.</summary>
    LayerNotUpdating,

    /// <summary>The page answered on neither channel.</summary>
    NoReplies,

    /// <summary>Script results are lost: Project Cook never learns the page needs its script again.</summary>
    ScriptRepliesLost,

    /// <summary>Page messages are lost: Better Trade and Item Totals never learn the page needs their script again.</summary>
    PageMessagesLost,

    /// <summary>All links work but a mod's script is not on the page. Normal until that mod's window has been opened once.</summary>
    ModScriptsMissing,
}

public static class PageLink
{
    public static PageLinkVerdict Decide(PageProbeResult probe)
    {
        if (!probe.IsLayerWebViewLive) return PageLinkVerdict.WebViewNotLive;

        if (probe.OtherLiveWebViews > 0) return PageLinkVerdict.StaleWebViewReference;

        if (probe.LayerUpdatesDuringProbe == 0) return PageLinkVerdict.LayerNotUpdating;

        if (!probe.HasScriptReply && !probe.HasMessageReply) return PageLinkVerdict.NoReplies;

        if (!probe.HasScriptReply) return PageLinkVerdict.ScriptRepliesLost;

        if (!probe.HasMessageReply) return PageLinkVerdict.PageMessagesLost;

        return probe.MissingModScripts.Count > 0 ? PageLinkVerdict.ModScriptsMissing : PageLinkVerdict.Healthy;
    }

    /// <summary>
    /// The page-script globals of <paramref name="loadedGlobals"/> that the probe reply reports absent.
    /// A reply that never arrived reports nothing missing: absence of evidence is a channel problem, judged elsewhere.
    /// </summary>
    public static IReadOnlyList<string> MissingModScripts(string? reply, IEnumerable<string> loadedGlobals)
    {
        if (reply is null) return Array.Empty<string>();

        return loadedGlobals.Where(name => !reply.Contains($"\"{name}\":true", StringComparison.Ordinal)).ToList();
    }
}
