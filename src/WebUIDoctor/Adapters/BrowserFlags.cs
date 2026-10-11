using System;
using Vuplex.WebView;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Hands Chromium the game's page-access flags again before a recovery starts it anew. The game sets them once at
/// startup, and a Chromium restarted by recovery comes up without them, which cuts every page mod off its frames.
/// </summary>
internal static class BrowserFlags
{
    public static void Restore(Action<string> log)
    {
        try
        {
            StandaloneWebView.SetCommandLineArguments(ChromiumProcess.PageAccessFlags);
            log("page-access flags handed to Chromium for the restart");

            return;
        }
        catch (Exception ex)
        {
            log("SetCommandLineArguments refused (" + ex.Message + "); trying the native call");
        }

        try
        {
            var isAccepted = StandaloneWebView.WebView_setCommandLineArguments(ChromiumProcess.PageAccessFlags);
            log("native setCommandLineArguments " + (isAccepted ? "accepted the page-access flags" : "refused the page-access flags"));
        }
        catch (Exception ex)
        {
            log("native setCommandLineArguments threw: " + ex.Message);
        }
    }
}
