using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Show;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using UnityEngine.UI;
using WebUIDoctor.Core;
using CameraTouch = GameCore.Scripts.CameraTouch;
using Time = UnityEngine.Time;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Reads whether clicks on the world still reach the game and gives them back. World clicks land on the full-screen
/// image of CameraTouch; the object menu, the trap panel and the vase panel switch its raycastTarget off when they open
/// (Reducer_Web_SelectMenu.RA_Open, Reducer_Web_TrapPanel.RA_Open, Reducer_Web_VasePanel.RA_Open) and on again when
/// they close. A broken reading is looked at again a moment later before anything is changed.
/// All members run on Unity's main thread.
/// </summary>
internal sealed class ClickWatch
{
    /// <summary>The game registers a panel only after its page has loaded, which can take more than a frame.</summary>
    private static readonly TimeSpan SecondLookDelay = TimeSpan.FromSeconds(1);

    private readonly Action<string> _log;
    private PendingLook? _pending;

    public ClickWatch(Action<string> log) => _log = log;

    private sealed record PendingLook(RepairTrigger Trigger, ClickSnapshot Earlier, int Frame, DateTime DueAt);

    /// <summary>First look, from the UI repair. Replaces a second look still waiting from an earlier repair.</summary>
    public void Check(RepairTrigger trigger, DateTime now)
    {
        _pending = null;
        LookSafely(trigger, null, now);
    }

    /// <summary>Takes the second look once at least one frame and <see cref="SecondLookDelay"/> have passed.</summary>
    public void Tick(DateTime now)
    {
        if (_pending is not { } pending || Time.frameCount <= pending.Frame || now < pending.DueAt) return;

        _pending = null;
        LookSafely(pending.Trigger, pending.Earlier, now);
    }

    /// <summary>Fail-soft: a read the game refuses is logged, and the rest of the UI repair still runs.</summary>
    private void LookSafely(RepairTrigger trigger, ClickSnapshot? earlier, DateTime now)
    {
        try
        {
            Look(trigger, earlier, now);
        }
        catch (Exception ex)
        {
            _log("click check failed: " + ex.Message);
        }
    }

    private void Look(RepairTrigger trigger, ClickSnapshot? earlier, DateTime now)
    {
        var world = WorldView.Find();

        if (world == null)
        {
            _log("click check: no world view loaded, nothing to check");

            return;
        }

        var current = world.Read();
        var repair = ClickHeal.Decide(current, earlier, trigger);
        var look = earlier == null ? "click check" : "click check, second look";
        _log($"{look}: {current} -> {repair}");

        if (repair.NeedsSecondLook) _pending = new PendingLook(trigger, current, Time.frameCount, now + SecondLookDelay);
        if (!repair.IsNeeded) return;

        world.Repair(repair, _log);
        _log($"click check after repair (a requested close lands on a later frame): {world.Read()}");
    }

    /// <summary>The game objects one click check reads and changes.</summary>
    private sealed class WorldView
    {
        private readonly Image _image;
        private readonly ReduxUISystem? _ui;
        private readonly CameraManager? _camera;

        private WorldView(Image image, ReduxUISystem? ui, CameraManager? camera)
        {
            _image = image;
            _ui = ui;
            _camera = camera;
        }

        public static WorldView? Find()
        {
            var touch = CameraTouch.Instance;
            var image = touch == null ? null : touch.GetComponent<Image>();

            if (image == null) return null;

            return new WorldView(image, GameSingleton.Existing<ReduxUISystem>(), GameSingleton.Existing<BattleShowWorld>()?._CameraManager);
        }

        public ClickSnapshot Read() => new(
            _image.raycastTarget,
            IsOpen(Il2CppType.Of<WebUI_SelectMenu>()),
            IsOpen(Il2CppType.Of<WebUI_TrapPanel>()),
            IsOpen(Il2CppType.Of<WebUI_VasePanel>()),
            _camera?.isBattleSelectOpen ?? false);

        /// <summary>Each step uses the game's own call for it; one failing does not stop the next.</summary>
        public void Repair(ClickRepair repair, Action<string> log)
        {
            if (repair.CloseSelectMenu) Step("asked the game to close the object menu", Ac_SelectMenu_Close.SendAction, log);
            if (repair.CloseTrapPanel) Step("asked the game to close the trap panel", Ac_TrapPanel_Close.SendAction, log);
            if (repair.CloseVasePanel) Step("asked the game to close the vase panel", Ac_VasePanel_Close.SendAction, log);
            if (repair.RestoreWorldClicks) Step("gave clicks on the world back", () => _image.raycastTarget = true, log);
            if (repair.ClearSelectFlag && _camera is { } camera) Step("cleared the camera's object-menu flag", () => camera.OnBattleSelectStateChange(false), log);
        }

        private bool IsOpen(Il2CppSystem.Type page) => _ui != null && _ui.IsUIActive(page);

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
}
