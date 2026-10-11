using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.Battle.Show;
using WebUIDoctor.Core;

namespace WebUIDoctor.Adapters;

/// <summary>Reads the game's pause state and releases a pause the crashed web UI left behind.</summary>
internal static class PauseState
{
    public static void Heal(RepairTrigger trigger, Action<string> log)
    {
        var world = BaseSingleton<BattleLogicWorld>.Instance;
        var camera = BaseSingleton<BattleShowWorld>.Instance?._CameraManager;

        if (world == null || camera == null)
        {
            log("pause check: no game world loaded, nothing to resume");

            return;
        }

        var before = Read(world, camera);
        var verdict = PauseHeal.Decide(before, trigger);
        log($"pause check: {before} -> {verdict}");

        if (before.IsWorldPaused) log("pause check: last pause came from: " + Flatten(world._lastPauseStack));

        if (verdict != PauseVerdict.Stuck) return;

        Resume(world, camera, before, log);
        log($"pause check after resume: {Read(world, camera)}");
    }

    private static PauseSnapshot Read(BattleLogicWorld world, CameraManager camera) => new(
        world.IsPause,
        camera.isTimePaused,
        camera._isRenderFrozen,
        world._GameTimeManager?.IsPausedScale() ?? false,
        world.IsInLegitPauseModal(),
        world._userUIPauseHolds);

    /// <summary>Each step uses the game's own call for it; one failing does not stop the next.</summary>
    private static void Resume(BattleLogicWorld world, CameraManager camera, PauseSnapshot before, Action<string> log)
    {
        Step("dropped the leftover window pause holds", world.ResetUserUIPauseHolds, log);

        if (before.IsWorldPaused) Step("resumed the world", world.OnResume, log);
        if (camera.isTimePaused) Step("resumed the camera", camera.OnTimeResume, log);
        if (camera._isRenderFrozen) Step("released the still frame", () => camera.UnfreezeRenderForPause(false), log);
    }

    private static void Step(string done, Action action, Action<string> log)
    {
        try
        {
            action();
            log("pause fix: " + done);
        }
        catch (Exception ex)
        {
            log($"pause fix failed ({done}): {ex.Message}");
        }
    }

    private static string Flatten(string? stack) => string.IsNullOrWhiteSpace(stack) ? "(not recorded)" : stack.Replace('\n', ' ').Replace('\r', ' ');
}
