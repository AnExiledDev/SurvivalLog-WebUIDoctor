using System;
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

    public static void Apply(Harmony harmony, Doctor doctor, PageProbe probe, Action<string> log)
    {
        _doctor = doctor;
        _probe = probe;
        _log = log;

        Patch(harmony, "OnUpdate", postfix: nameof(OnUpdatePostfix));
        Patch(harmony, "NotifyRecoverAbandoned", prefix: nameof(NotifyRecoverAbandonedPrefix));
        Patch(harmony, "RecoverWebView", prefix: nameof(RecoverWebViewPrefix));
        Patch(harmony, "OnWebViewTerminated", prefix: nameof(OnWebViewTerminatedPrefix));
        Patch(harmony, "OnBrowserProcessFailed", prefix: nameof(OnBrowserProcessFailedPrefix));
        Patch(harmony, "LogInitFailure", prefix: nameof(LogInitFailurePrefix));
        Patch(harmony, "ReportWebUICrashDiagnostic", prefix: nameof(ReportWebUICrashDiagnosticPrefix));
        Patch(harmony, "OnPageFailed", prefix: nameof(OnPageFailedPrefix));
        Patch(harmony, "ClassifyDiagnosis", postfix: nameof(ClassifyDiagnosisPostfix));
        PatchPageMessages(harmony);
    }

    /// <summary>Runs first so the probe's own page messages never reach the game or the other mods' hooks.</summary>
    private static void PatchPageMessages(Harmony harmony)
    {
        try
        {
            var original = AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted") ??
                           throw new MissingMethodException(nameof(BaseWebView), "HandleMessageEmitted");

            harmony.Patch(original, prefix: new HarmonyMethod(typeof(WebUILayerPatches), nameof(HandleMessageEmittedPrefix)) { priority = Priority.First });
            _log("hooked BaseWebView.HandleMessageEmitted");
        }
        catch (Exception ex)
        {
            _log($"could not hook BaseWebView.HandleMessageEmitted: {ex.Message}");
        }
    }

    private static void Patch(Harmony harmony, string target, string? prefix = null, string? postfix = null)
    {
        try
        {
            var original = AccessTools.Method(typeof(WebUILayer), target) ?? throw new MissingMethodException(nameof(WebUILayer), target);

            harmony.Patch(original, prefix: Hook(prefix), postfix: Hook(postfix));
            _log($"hooked WebUILayer.{target}");
        }
        catch (Exception ex)
        {
            _log($"could not hook WebUILayer.{target}: {ex.Message}");
        }
    }

    private static HarmonyMethod? Hook(string? name) => name is null ? null : new HarmonyMethod(typeof(WebUILayerPatches), name);

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

    private static void OnUpdatePostfix(WebUILayer __instance) =>
        Guard("OnUpdate", () =>
        {
            _doctor.SeeLayer(__instance);
            _probe.CountLayerUpdate();
        });

    private static bool HandleMessageEmittedPrefix(string serializedMessage)
    {
        var shouldPassOn = true;

        Guard("HandleMessageEmitted", () => shouldPassOn = _probe.OnPageMessage(serializedMessage));

        return shouldPassOn;
    }

    private static bool NotifyRecoverAbandonedPrefix(WebUILayer __instance)
    {
        var shouldSkipDialog = false;

        Guard("NotifyRecoverAbandoned", () => shouldSkipDialog = _doctor.InterceptAbandon(__instance));

        return !shouldSkipDialog;
    }

    private static void RecoverWebViewPrefix(WebUILayer __instance) =>
        Guard("RecoverWebView", () => _doctor.OnOurRecoveryStarted(__instance));

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

    private static void ClassifyDiagnosisPostfix(
        string __result, string stage, string childExitCode, string createProcessHr, string faultingModule,
        bool oomInLog, bool gpuFatal, bool gpuContextLost, bool gpuSkiaOom) =>
        Guard("ClassifyDiagnosis", () => _log(
            $"game diagnosis: {__result} | stage={stage} childExit={childExitCode} createProcessHr={createProcessHr} " +
            $"faultingModule={faultingModule} oomInLog={oomInLog} gpuFatal={gpuFatal} gpuContextLost={gpuContextLost} gpuSkiaOom={gpuSkiaOom}"));
}
