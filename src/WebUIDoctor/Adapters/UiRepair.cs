using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebUIDoctor.Core;
using Object = UnityEngine.Object;

namespace WebUIDoctor.Adapters;

/// <summary>
/// Cleans up after the game gave up on its UI: closes the "automatic recovery has failed" dialog and gives
/// clicks back to the web UI. The dialog is native Unity UI drawn over the browser, which is why the recovered
/// UI still answers the keyboard but not the mouse. Everything it sees and does is logged.
/// </summary>
internal static class UiRepair
{
    private const int SnippetLength = 80;

    /// <summary>Gives clicks back and asks the game to close its error screen. <see cref="Finish"/> runs a moment later.</summary>
    public static void Start(WebUILayer layer, RepairTrigger trigger, Action<string> log)
    {
        log($"UI repair ({trigger}) starting");
        RestoreClicks(layer, log);
        CloseViaGameAction(log);
    }

    /// <summary>
    /// Hides the error screen only if the game's own close left it up (hiding it bypasses the game's bookkeeping,
    /// which is how 0.2.1 left the world paused), then releases any pause the crash left behind and checks whether a
    /// panel that never appeared took the clicks on the world.
    /// </summary>
    public static void Finish(WebUILayer layer, RepairTrigger trigger, ClickWatch clicks, DateTime now, Action<string> log)
    {
        var hidden = CloseErrorDialogs(layer, log);

        log(hidden == 0 ? "UI repair: no error dialog left on screen" : $"UI repair: the game's close action left {hidden} dialog panel(s) up; hid them by hand");
        PauseState.Heal(trigger, log);
        clicks.Check(trigger, now);
        DumpCanvases(layer, log);
        log($"UI repair ({trigger}) done");
    }

    /// <summary>The give-up opens the game's own UI_WebInitFailure screen through a Redux action; its close action is the clean way out.</summary>
    private static void CloseViaGameAction(Action<string> log)
    {
        try
        {
            Ac_WebInitFailure_CloseUI.SendAction();
            log("UI repair: sent the game's close action for the recovery-failure screen");
        }
        catch (Exception ex)
        {
            log("UI repair: the game's close action failed: " + ex.Message);
        }
    }

    public static void DumpCanvases(WebUILayer layer, Action<string> log)
    {
        var webUiCanvasId = WebUiRootCanvasId(layer);

        foreach (var canvas in FindAll<Canvas>().Where(c => c.isRootCanvas))
        {
            var texts = TextsUnder(canvas.gameObject).Where(t => !string.IsNullOrWhiteSpace(t.Text)).Take(6).Select(t => Snippet(t.Text));
            var role = canvas.GetInstanceID() == webUiCanvasId ? " [web UI]" : "";

            log($"  canvas '{canvas.gameObject.name}'{role} active={canvas.gameObject.activeInHierarchy} enabled={canvas.enabled} " +
                $"order={canvas.sortingOrder} mode={canvas.renderMode} raycaster={HasRaycaster(canvas)} texts=[{string.Join(" | ", texts)}]");
        }
    }

    private static void RestoreClicks(WebUILayer layer, Action<string> log)
    {
        foreach (var (name, group) in new[] { ("web UI", layer.canvasGroup), ("cinematic", layer.cinematicGroup) })
        {
            if (group == null) continue;

            log($"UI repair: {name} CanvasGroup was alpha={group.alpha:0.##} interactable={group.interactable} blocksRaycasts={group.blocksRaycasts}");
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private static int CloseErrorDialogs(WebUILayer layer, Action<string> log)
    {
        var webUiCanvasId = WebUiRootCanvasId(layer);
        var panels = new Dictionary<int, GameObject>();

        foreach (var (text, owner) in AllTexts())
        {
            if (!owner.activeInHierarchy || !ErrorDialogText.IsRecoveryFailureDialog(text)) continue;

            var canvas = owner.GetComponentInParent<Canvas>();

            if (canvas == null || canvas.rootCanvas.GetInstanceID() == webUiCanvasId) continue;

            var panel = TopPanelUnder(canvas.rootCanvas.transform, owner.transform);

            panels.TryAdd(panel.GetInstanceID(), panel);
        }

        foreach (var panel in panels.Values)
        {
            log($"UI repair: hiding '{PathOf(panel.transform)}'");
            panel.SetActive(false);
        }

        return panels.Count;
    }

    /// <summary>The child of the root canvas that holds the text: the dialog itself, not a canvas it may share.</summary>
    private static GameObject TopPanelUnder(Transform root, Transform node)
    {
        var current = node;

        while (current.parent != null && current.parent != root) current = current.parent;

        return current.parent == root ? current.gameObject : node.gameObject;
    }

    private static int WebUiRootCanvasId(WebUILayer layer)
    {
        var prefab = layer.canvasWebViewPrefab;

        if (prefab == null) return 0;

        var canvas = prefab.GetComponentInParent<Canvas>();

        return canvas == null ? 0 : canvas.rootCanvas.GetInstanceID();
    }

    private static IEnumerable<(string Text, GameObject Owner)> AllTexts()
    {
        foreach (var text in FindAll<TMP_Text>()) yield return (text.text ?? "", text.gameObject);
        foreach (var text in FindAll<Text>()) yield return (text.text ?? "", text.gameObject);
    }

    private static IEnumerable<(string Text, GameObject Owner)> TextsUnder(GameObject root)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) yield return (text.text ?? "", text.gameObject);
        foreach (var text in root.GetComponentsInChildren<Text>(true)) yield return (text.text ?? "", text.gameObject);
    }

    private static IEnumerable<T> FindAll<T>() where T : Object
    {
        var found = Object.FindObjectsByType(Il2CppType.Of<T>(), FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var item in found)
        {
            var typed = item.TryCast<T>();

            if (typed != null) yield return typed;
        }
    }

    private static bool HasRaycaster(Canvas canvas) => canvas.GetComponent<GraphicRaycaster>() != null;

    private static string PathOf(Transform node) => node.parent == null ? node.name : PathOf(node.parent) + "/" + node.name;

    private static string Snippet(string text)
    {
        var flat = text.Replace('\n', ' ').Replace('\r', ' ').Trim();

        return flat.Length <= SnippetLength ? flat : flat[..SnippetLength] + "...";
    }
}
