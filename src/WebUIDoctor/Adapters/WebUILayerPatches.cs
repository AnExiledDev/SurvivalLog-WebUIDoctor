using System;
using ExiledToolkit.Game;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Vuplex.WebView;
using Vuplex.WebView.Internal;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Harmony hooks on the game's WebUI layer. Each target is patched on its own so a renamed or missing
/// method costs only that hook. Every hook swallows its own exceptions: a diagnostic must never be the crash.
/// </summary>
internal static class WebUILayerPatches
{
    private static Doctor _doctor = null!;
    private static PageProbe _probe = null!;
    private static Action<string> _log = _ => { };

    public static void Apply(PatchKit hooks, Doctor doctor, PageProbe probe, Action<string> log)
    {
        _doctor = doctor;
        _probe = probe;
        _log = log;

        Patch(hooks, "OnUpdate", postfix: nameof(OnUpdatePostfix));
        Patch(hooks, "NotifyRecoverAbandoned", prefix: nameof(NotifyRecoverAbandonedPrefix));
        Patch(hooks, "RecoverWebView", prefix: nameof(RecoverWebViewPrefix));
        Patch(hooks, "StartChromiumPipeWatchdog", prefix: nameof(StartChromiumPipeWatchdogPrefix));
        Patch(hooks, "OnWebViewTerminated", prefix: nameof(OnWebViewTerminatedPrefix));
        Patch(hooks, "OnBrowserProcessFailed", prefix: nameof(OnBrowserProcessFailedPrefix));
        Patch(hooks, "LogInitFailure", prefix: nameof(LogInitFailurePrefix));
        Patch(hooks, "ReportWebUICrashDiagnostic", prefix: nameof(ReportWebUICrashDiagnosticPrefix));
        Patch(hooks, "OnPageFailed", prefix: nameof(OnPageFailedPrefix));
        PatchPageMessages(hooks);

        // Never hook ClassifyDiagnosis: it takes Nullable<bool> by value, which Il2CppInterop's trampoline reads
        // as a pointer, so every call throws before any patch runs and the game's crash report loses its result.
        // The game logs the diagnosis itself ("WebUI: ... stage= diag= ...") once that call returns.
    }

    /// <summary>
    /// Runs first so the probe's own page messages never reach the game or the other mods' hooks. A hook of its own
    /// rather than the Toolkit's page inbox, because it must run before every other hook and it also counts the other
    /// mods' messages that pass by during a probe.
    /// </summary>
    private static void PatchPageMessages(PatchKit hooks) =>
        hooks.Patch(typeof(WebUILayerPatches), typeof(BaseWebView), "HandleMessageEmitted", null, Priority.First,
            prefix: nameof(HandleMessageEmittedPrefix), done: "hooked BaseWebView.HandleMessageEmitted");

    private static void Patch(PatchKit hooks, string target, string? prefix = null, string? postfix = null) =>
        hooks.Patch(typeof(WebUILayerPatches), typeof(WebUILayer), target, prefix: prefix, postfix: postfix, done: $"hooked WebUILayer.{target}");

    private static void Guard(string hook, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            _log($"{hook} hook failed: {ex.Message}");
        }
    }

    // The two hooks below run every frame and on every page message, so they skip Guard's closure allocation.
    private static void OnUpdatePostfix(WebUILayer __instance)
    {
        try
        {
            _doctor.SeeLayer(__instance);
            _probe.CountLayerUpdate();
        }
        catch (Exception ex)
        {
            _log("OnUpdate hook failed: " + ex.Message);
        }
    }

    private static bool HandleMessageEmittedPrefix(string serializedMessage)
    {
        try
        {
            return _probe.OnPageMessage(serializedMessage);
        }
        catch (Exception ex)
        {
            _log("HandleMessageEmitted hook failed: " + ex.Message);

            return true;
        }
    }

    private static bool NotifyRecoverAbandonedPrefix(WebUILayer __instance)
    {
        var shouldSkipDialog = false;

        Guard("NotifyRecoverAbandoned", () => shouldSkipDialog = _doctor.InterceptAbandon(__instance));

        return !shouldSkipDialog;
    }

    private static void RecoverWebViewPrefix(WebUILayer __instance) =>
        Guard("RecoverWebView", () => _doctor.OnOurRecoveryStarted(__instance));

    private static void StartChromiumPipeWatchdogPrefix(WebUILayer __instance) =>
        Guard("StartChromiumPipeWatchdog", () => _doctor.OnBrowserAboutToStart(__instance));

    private static void OnWebViewTerminatedPrefix(WebUILayer __instance, TerminatedEventArgs e) =>
        Guard("OnWebViewTerminated", () =>
        {
            _doctor.SeeLayer(__instance);
            _log($"game saw the UI page terminate: type={e?.Type.ToString() ?? "null"}");
        });

    private static void OnBrowserProcessFailedPrefix(WebUILayer __instance, BrowserProcessFailedEventArgs e) =>
        Guard("OnBrowserProcessFailed", () =>
        {
            _doctor.SeeLayer(__instance);
            _log($"game saw the browser process fail: {e?.ErrorMessage ?? "null"}");
        });

    private static void LogInitFailurePrefix(string reason) =>
        Guard("LogInitFailure", () => _log($"game WebUI init failure: {reason}"));

    private static void ReportWebUICrashDiagnosticPrefix(string reason) =>
        Guard("ReportWebUICrashDiagnostic", () => _log($"game crash diagnostic: {reason}"));

    private static void OnPageFailedPrefix(string pageId) =>
        Guard("OnPageFailed", () => _log($"game page failed: {pageId}"));
}
