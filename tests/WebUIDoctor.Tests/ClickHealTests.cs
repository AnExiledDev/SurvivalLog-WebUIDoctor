using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ClickHealTests
{
    private static readonly ClickSnapshot Healthy = new(
        IsWorldClickable: true, IsSelectMenuOpen: false, IsTrapPanelOpen: false, IsVasePanelOpen: false, IsSelectMenuFlagged: false);

    // 2026-10-12: objects stopped opening their menu after an interface reload. Player.log shows two page loads dropped
    // while the browser was down; the object menu switches world clicks off when it opens (Reducer_Web_SelectMenu.RA_Open),
    // and with its page dropped it never shows and never closes.
    private static readonly ClickSnapshot OrphanedMenu = Healthy with { IsWorldClickable = false, IsSelectMenuFlagged = true };

    private static readonly ClickSnapshot Blocked = Healthy with { IsWorldClickable = false };

    /// <summary>The same reading on both looks, which is what lets a repair act.</summary>
    private static ClickRepair DecideSeenTwice(ClickSnapshot snapshot, RepairTrigger trigger) => ClickHeal.Decide(snapshot, snapshot, trigger);

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void World_clicks_switched_off_by_a_menu_that_never_opened_are_given_back(RepairTrigger trigger)
    {
        var repair = DecideSeenTwice(OrphanedMenu, trigger);

        Assert.True(repair.RestoreWorldClicks);
        Assert.True(repair.ClearSelectFlag);
        Assert.False(repair.CloseSelectMenu || repair.CloseTrapPanel || repair.CloseVasePanel);
    }

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void Clicks_that_look_switched_off_on_the_first_look_are_only_looked_at_again(RepairTrigger trigger)
    {
        // A panel still opening has already switched clicks off but is not registered yet, which reads the same.
        Assert.Equal(ClickRepair.LookAgain, ClickHeal.Decide(OrphanedMenu, earlier: null, trigger));
        Assert.False(ClickRepair.LookAgain.IsNeeded);
    }

    [Fact]
    public void A_reading_that_changed_between_the_two_looks_is_left_to_the_game()
    {
        var menuStillOpening = OrphanedMenu;
        var menuNowRegistered = OrphanedMenu with { IsSelectMenuOpen = true };

        Assert.Equal(ClickRepair.None, ClickHeal.Decide(menuNowRegistered, menuStillOpening, RepairTrigger.Manual));
        Assert.Equal(ClickRepair.None, ClickHeal.Decide(OrphanedMenu, Blocked, RepairTrigger.Manual));
    }

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void A_world_that_still_takes_clicks_is_left_alone_without_a_second_look(RepairTrigger trigger)
    {
        Assert.Equal(ClickRepair.None, ClickHeal.Decide(Healthy, earlier: null, trigger));
        Assert.Equal(ClickRepair.None, DecideSeenTwice(Healthy, trigger));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void After_an_automatic_repair_a_panel_the_game_says_is_open_keeps_the_clicks(bool isSelectMenuOpen, bool isTrapPanelOpen, bool isVasePanelOpen)
    {
        var panelUp = Blocked with
        {
            IsSelectMenuOpen = isSelectMenuOpen, IsTrapPanelOpen = isTrapPanelOpen, IsVasePanelOpen = isVasePanelOpen, IsSelectMenuFlagged = isSelectMenuOpen,
        };

        Assert.Equal(ClickRepair.None, DecideSeenTwice(panelUp, RepairTrigger.AfterRecovery));
    }

    [Fact]
    public void Ctrl_F12_closes_only_the_object_menu_when_it_alone_holds_the_clicks()
    {
        var menuUp = Blocked with { IsSelectMenuOpen = true, IsSelectMenuFlagged = true };

        Assert.Equal(new ClickRepair(true, false, false, true, true), DecideSeenTwice(menuUp, RepairTrigger.Manual));
    }

    [Fact]
    public void Ctrl_F12_closes_only_the_trap_panel_when_it_alone_holds_the_clicks()
    {
        var trapUp = Blocked with { IsTrapPanelOpen = true };

        Assert.Equal(new ClickRepair(false, true, false, true, false), DecideSeenTwice(trapUp, RepairTrigger.Manual));
    }

    [Fact]
    public void Ctrl_F12_closes_only_the_vase_panel_when_it_alone_holds_the_clicks()
    {
        var vaseUp = Blocked with { IsVasePanelOpen = true };

        Assert.Equal(new ClickRepair(false, false, true, true, false), DecideSeenTwice(vaseUp, RepairTrigger.Manual));
    }

    [Fact]
    public void Ctrl_F12_closes_every_panel_holding_the_clicks_and_gives_them_back()
    {
        var allUp = Blocked with { IsSelectMenuOpen = true, IsTrapPanelOpen = true, IsVasePanelOpen = true, IsSelectMenuFlagged = true };

        Assert.Equal(new ClickRepair(true, true, true, true, true), DecideSeenTwice(allUp, RepairTrigger.Manual));
    }

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void A_camera_still_flagging_an_object_menu_that_is_not_open_is_cleared(RepairTrigger trigger)
    {
        var staleFlag = Healthy with { IsSelectMenuFlagged = true };

        Assert.Equal(ClickRepair.None with { ClearSelectFlag = true }, DecideSeenTwice(staleFlag, trigger));
    }

    [Fact]
    public void The_flag_of_an_object_menu_that_is_really_open_is_kept()
    {
        var menuUp = Healthy with { IsSelectMenuOpen = true, IsSelectMenuFlagged = true };

        Assert.Equal(ClickRepair.None, DecideSeenTwice(menuUp, RepairTrigger.Manual));
    }
}
