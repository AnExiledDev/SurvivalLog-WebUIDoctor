using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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
/// <param name="BlockedFrames">Page frames whose document the game's Root page could not read.</param>
public readonly record struct PageProbeResult(
    bool IsLayerWebViewLive,
    int OtherLiveWebViews,
    int LayerUpdatesDuringProbe,
    bool HasScriptReply,
    bool HasMessageReply,
    IReadOnlyList<string> MissingModScripts,
    IReadOnlyList<BlockedFrame> BlockedFrames);

/// <summary>A page frame the Root page could not reach into, and the error the read threw (a SecurityError when Chromium runs without its page-access flags).</summary>
public readonly record struct BlockedFrame(string Frame, string Error)
{
    public override string ToString() => $"{Frame} ({Error})";
}

public enum PageLinkVerdict
{
    /// <summary>Every link works, every frame is readable and every loaded mod's script is on the page.</summary>
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

    /// <summary>
    /// Broken: the page answers, but its frames are closed to it, so every mod that adds to a game window fails
    /// whatever the channels say. What a browser relaunched without its page-access flags looks like.
    /// </summary>
    FramesBlocked,

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

        if (probe.BlockedFrames.Count > 0) return PageLinkVerdict.FramesBlocked;

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

    /// <summary>
    /// The frames the probe reply's "blocked" list names: each one's document threw when the Root page read it.
    /// A reply that is missing, not JSON, or has no such list blocks nothing.
    /// </summary>
    public static IReadOnlyList<BlockedFrame> BlockedFrames(string? reply)
    {
        if (string.IsNullOrEmpty(reply)) return Array.Empty<BlockedFrame>();

        try
        {
            using var document = JsonDocument.Parse(reply);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("blocked", out var blocked) || blocked.ValueKind != JsonValueKind.Array)
                return Array.Empty<BlockedFrame>();

            return blocked.EnumerateArray().Select(entry => new BlockedFrame(TextOf(entry, "frame"), TextOf(entry, "error"))).ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<BlockedFrame>();
        }
    }

    private static string TextOf(JsonElement entry, string property) =>
        entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
