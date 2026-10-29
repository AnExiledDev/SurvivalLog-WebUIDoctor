using System;
using BepInEx.Configuration;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Raises the browser frame rate the game picks for its quality tier. The game reads it when it starts the browser
/// (ResolveChromiumLaunchConfig) and again when its settings screen checks whether a restart is pending
/// (IsWebFrameRateRestartPending), so both see the same value and no restart is reported for it.
/// </summary>
internal static class WebFrameRatePatch
{
    private const string Target = "GetTargetWebFrameRateForTier";

    private static ConfigEntry<int> _rate = null!;
    private static Action<string> _log = _ => { };
    private static uint _lastLogged;

    public static void Apply(Harmony harmony, ConfigEntry<int> rate, Action<string> log)
    {
        _rate = rate;
        _log = log;

        try
        {
            var original = AccessTools.Method(typeof(WebUILayer), Target) ?? throw new MissingMethodException(nameof(WebUILayer), Target);

            harmony.Patch(original, postfix: new HarmonyMethod(typeof(WebFrameRatePatch), nameof(Postfix)));
            _log($"hooked WebUILayer.{Target}");
        }
        catch (Exception ex)
        {
            _log($"{HookFailure.Prefix} WebUILayer.{Target}: {ex.Message}");
        }
    }

    private static void Postfix(string tierName, ref uint __result)
    {
        try
        {
            var gameRate = __result;

            __result = WebFrameRate.Pick(_rate.Value);

            if (__result == _lastLogged) return;

            _lastLogged = __result;
            _log($"menu frame rate {__result} (the game picked {gameRate} for quality tier {tierName})");
        }
        catch (Exception ex)
        {
            _log($"{Target} hook failed: {ex.Message}");
        }
    }
}
