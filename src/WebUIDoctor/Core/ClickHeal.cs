namespace WebUIDoctor.Core;

/// <summary>What the game says about clicks on the world, read after a UI repair.</summary>
/// <param name="IsWorldClickable">The full-screen CameraTouch image still catches clicks; the menus open from there.</param>
/// <param name="IsSelectMenuOpen">The game has the object menu (explore, sleep, ...) registered as open.</param>
/// <param name="IsTrapPanelOpen">The game has the trap panel registered as open.</param>
/// <param name="IsVasePanelOpen">The game has the vase panel registered as open.</param>
/// <param name="IsSelectMenuFlagged">CameraManager.isBattleSelectOpen: the camera thinks an object menu is up.</param>
public readonly record struct ClickSnapshot(
    bool IsWorldClickable,
    bool IsSelectMenuOpen,
    bool IsTrapPanelOpen,
    bool IsVasePanelOpen,
    bool IsSelectMenuFlagged)
{
    /// <summary>The object menu, the trap panel and the vase panel each switch world clicks off while they are open.</summary>
    public bool IsPanelOpen => IsSelectMenuOpen || IsTrapPanelOpen || IsVasePanelOpen;

    public override string ToString() =>
        $"worldClickable={IsWorldClickable} selectMenuOpen={IsSelectMenuOpen} trapPanelOpen={IsTrapPanelOpen} " +
        $"vasePanelOpen={IsVasePanelOpen} selectFlag={IsSelectMenuFlagged}";
}

/// <summary>The steps that give clicks on the world back; each one is the game's own way of undoing a panel.</summary>
/// <param name="NeedsSecondLook">Nothing is done yet: the same reading has to be seen again a moment later first.</param>
public readonly record struct ClickRepair(
    bool CloseSelectMenu,
    bool CloseTrapPanel,
    bool CloseVasePanel,
    bool RestoreWorldClicks,
    bool ClearSelectFlag,
    bool NeedsSecondLook = false)
{
    public static readonly ClickRepair None = new(false, false, false, false, false);

    public static readonly ClickRepair LookAgain = None with { NeedsSecondLook = true };

    public bool IsNeeded => CloseSelectMenu || CloseTrapPanel || CloseVasePanel || RestoreWorldClicks || ClearSelectFlag;

    public override string ToString()
    {
        if (NeedsSecondLook) return "looking again before acting";

        return IsNeeded
            ? $"closeSelectMenu={CloseSelectMenu} closeTrapPanel={CloseTrapPanel} closeVasePanel={CloseVasePanel} " +
              $"restoreWorldClicks={RestoreWorldClicks} clearSelectFlag={ClearSelectFlag}"
            : "nothing to do";
    }
}

/// <summary>
/// Decides how to give clicks on the world back. Opening the object menu, the trap panel or the vase panel switches world
/// clicks off until that panel closes. A click made while the browser was down still switches them off, but the page load
/// is dropped, so no panel ever shows and nothing ever closes it: every object stays unclickable.
/// </summary>
public static class ClickHeal
{
    /// <param name="current">The reading just taken.</param>
    /// <param name="earlier">The reading a moment before, or null on the first look.</param>
    /// <param name="trigger">What started the repair.</param>
    public static ClickRepair Decide(ClickSnapshot current, ClickSnapshot? earlier, RepairTrigger trigger)
    {
        var repair = Plan(current, trigger);

        if (!repair.IsNeeded) return ClickRepair.None;

        // A panel switches clicks off before the game registers it as open, so one reading of a panel still
        // opening looks exactly like one that never will. Only the same reading seen twice is acted on.
        if (earlier is not { } seenBefore) return ClickRepair.LookAgain;

        // A reading that changed in between is the game still opening or closing something: leave it to the game.
        return seenBefore == current ? repair : ClickRepair.None;
    }

    private static ClickRepair Plan(ClickSnapshot snapshot, RepairTrigger trigger)
    {
        var leaveClicksAlone = ClickRepair.None with { ClearSelectFlag = IsSelectFlagStale(snapshot) };

        if (snapshot.IsWorldClickable) return leaveClicksAlone;

        // After an automatic repair an open panel is believed: it is the reason clicks are off, and closing it is the player's call.
        if (snapshot.IsPanelOpen && trigger == RepairTrigger.AfterRecovery) return leaveClicksAlone;

        // Ctrl+F12 is only pressed when the player is stuck, so a panel still holding the clicks is closed the game's way.
        return new ClickRepair(
            CloseSelectMenu: snapshot.IsSelectMenuOpen,
            CloseTrapPanel: snapshot.IsTrapPanelOpen,
            CloseVasePanel: snapshot.IsVasePanelOpen,
            RestoreWorldClicks: true,
            ClearSelectFlag: snapshot.IsSelectMenuFlagged);
    }

    private static bool IsSelectFlagStale(ClickSnapshot snapshot) => snapshot.IsSelectMenuFlagged && !snapshot.IsSelectMenuOpen;
}
