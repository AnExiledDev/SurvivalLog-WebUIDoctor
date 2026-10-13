using System;
using Vuplex.WebView;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Hands Chromium its launch arguments again before the game starts it anew. The game sets them once at startup,
/// and a Chromium relaunched by a recovery comes up without them, which cuts every page mod off its frames. Vuplex
/// refuses arguments while its browser process counts as running, so this only works between the game's
/// TerminateBrowserProcess and its next webview.
/// </summary>
internal static class BrowserFlags
{
    public static void HandOver(string arguments, Action<string> log)
    {
        try
        {
            StandaloneWebView.SetCommandLineArguments(arguments);
            log($"Chromium launch arguments handed over for the next browser start: {arguments}");

            return;
        }
        catch (Exception ex)
        {
            log("SetCommandLineArguments refused (" + ex.Message + "); trying the native call");
        }

        try
        {
            var isAccepted = StandaloneWebView.WebView_setCommandLineArguments(arguments);
            log("native setCommandLineArguments " + (isAccepted ? "accepted" : "refused") + $" the launch arguments: {arguments}");
        }
        catch (Exception ex)
        {
            log("native setCommandLineArguments threw: " + ex.Message);
        }
    }
}
