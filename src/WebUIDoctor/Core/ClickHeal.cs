namespace WebUIDoctor.Core;

/// <summary>What the game says about clicks on the world, read after a UI repair.</summary>
/// <param name="IsWorldClickable">The full-screen CameraTouch image still catches clicks; the menus open from there.</param>
/// <param name="IsSelectMenuOpen">The game has the object menu (explore, sleep, ...) registered as open.</param>
/// <param name="IsTrapPanelOpen">The game has the trap panel registered as open.</param>
/// <param name="IsSelectMenuFlagged">CameraManager.isBattleSelectOpen: the camera thinks an object menu is up.</param>
public readonly record struct ClickSnapshot(
    bool IsWorldClickable,
    bool IsSelectMenuOpen,
    bool IsTrapPanelOpen,
    bool IsSelectMenuFlagged)
{
    public bool IsPanelOpen => IsSelectMenuOpen || IsTrapPanelOpen;

    public override string ToString() =>
        $"worldClickable={IsWorldClickable} selectMenuOpen={IsSelectMenuOpen} trapPanelOpen={IsTrapPanelOpen} selectFlag={IsSelectMenuFlagged}";
}

/// <summary>The steps that give clicks on the world back; each one is the game's own way of undoing a menu.</summary>
public readonly record struct ClickRepair(
    bool CloseSelectMenu,
    bool CloseTrapPanel,
    bool RestoreWorldClicks,
    bool ClearSelectFlag)
{
    public static readonly ClickRepair None = new(false, false, false, false);

    public bool IsNeeded => this != None;

    public override string ToString() => IsNeeded
        ? $"closeSelectMenu={CloseSelectMenu} closeTrapPanel={CloseTrapPanel} restoreWorldClicks={RestoreWorldClicks} clearSelectFlag={ClearSelectFlag}"
        : "nothing to do";
}

/// <summary>
/// Decides how to give clicks on the world back. Opening the object menu or the trap panel switches world clicks off
/// until that panel closes. A click made while the browser was down still switches them off, but the page load is
/// dropped, so no panel ever shows and nothing ever closes it: every object stays unclickable.
/// </summary>
public static class ClickHeal
{
    public static ClickRepair Decide(ClickSnapshot snapshot, RepairTrigger trigger)
    {
        var leaveClicksAlone = ClickRepair.None with { ClearSelectFlag = IsSelectFlagStale(snapshot) };

        if (snapshot.IsWorldClickable) return leaveClicksAlone;

        // After an automatic repair an open panel is believed: it is the reason clicks are off, and closing it is the player's call.
        if (snapshot.IsPanelOpen && trigger == RepairTrigger.AfterRecovery) return leaveClicksAlone;

        // Ctrl+F12 is only pressed when the player is stuck, so a panel still holding the clicks is closed the game's way.
        return new ClickRepair(
            CloseSelectMenu: snapshot.IsSelectMenuOpen,
            CloseTrapPanel: snapshot.IsTrapPanelOpen,
            RestoreWorldClicks: true,
            ClearSelectFlag: snapshot.IsSelectMenuFlagged);
    }

    private static bool IsSelectFlagStale(ClickSnapshot snapshot) => snapshot.IsSelectMenuFlagged && !snapshot.IsSelectMenuOpen;
}
