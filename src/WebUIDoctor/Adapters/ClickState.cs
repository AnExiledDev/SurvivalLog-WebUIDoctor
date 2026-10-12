using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Show;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using UnityEngine.UI;
using WebUIDoctor.Core;
using CameraTouch = GameCore.Scripts.CameraTouch;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Reads whether clicks on the world still reach the game and gives them back. World clicks land on the full-screen
/// image of CameraTouch; the object menu and the trap panel switch its raycastTarget off while they are open
/// (Reducer_Web_SelectMenu.RA_Open, Reducer_Web_TrapPanel.RA_Open) and on again when they close.
/// </summary>
internal static class ClickState
{
    /// <summary>Fail-soft: a read the game refuses is logged, and the rest of the UI repair still runs.</summary>
    public static void Heal(RepairTrigger trigger, Action<string> log)
    {
        try
        {
            CheckAndRepair(trigger, log);
        }
        catch (Exception ex)
        {
            log("click check failed: " + ex.Message);
        }
    }

    private static void CheckAndRepair(RepairTrigger trigger, Action<string> log)
    {
        var touch = CameraTouch.Instance;
        var image = touch == null ? null : touch.GetComponent<Image>();

        if (image == null)
        {
            log("click check: no world view loaded, nothing to check");

            return;
        }

        var ui = GameSingleton.Existing<ReduxUISystem>();
        var camera = GameSingleton.Existing<BattleShowWorld>()?._CameraManager;
        var before = Read(image, ui, camera);
        var repair = ClickHeal.Decide(before, trigger);
        log($"click check: {before} -> {repair}");

        if (!repair.IsNeeded) return;

        Repair(repair, image, camera, log);
        log($"click check after repair: {Read(image, ui, camera)}");
    }

    private static ClickSnapshot Read(Image image, ReduxUISystem? ui, CameraManager? camera) => new(
        image.raycastTarget,
        IsOpen(ui, Il2CppType.Of<WebUI_SelectMenu>()),
        IsOpen(ui, Il2CppType.Of<WebUI_TrapPanel>()),
        camera?.isBattleSelectOpen ?? false);

    private static bool IsOpen(ReduxUISystem? ui, Il2CppSystem.Type page) => ui != null && ui.IsUIActive(page);

    /// <summary>Each step uses the game's own call for it; one failing does not stop the next.</summary>
    private static void Repair(ClickRepair repair, Image image, CameraManager? camera, Action<string> log)
    {
        if (repair.CloseSelectMenu) Step("closed the object menu", Ac_SelectMenu_Close.SendAction, log);
        if (repair.CloseTrapPanel) Step("closed the trap panel", Ac_TrapPanel_Close.SendAction, log);
        if (repair.RestoreWorldClicks) Step("gave clicks on the world back", () => image.raycastTarget = true, log);
        if (repair.ClearSelectFlag && camera is { } view) Step("cleared the camera's object-menu flag", () => view.OnBattleSelectStateChange(false), log);
    }

    private static void Step(string done, Action action, Action<string> log)
    {
        try
        {
            action();
            log("click fix: " + done);
        }
        catch (Exception ex)
        {
            log($"click fix failed ({done}): {ex.Message}");
        }
    }
}
