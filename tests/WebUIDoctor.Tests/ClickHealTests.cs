using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ClickHealTests
{
    private static readonly ClickSnapshot Healthy = new(
        IsWorldClickable: true, IsSelectMenuOpen: false, IsTrapPanelOpen: false, IsSelectMenuFlagged: false);

    // 2026-10-12: objects stopped opening their menu after an interface reload. Player.log shows two page loads dropped
    // while the browser was down; the object menu switches world clicks off when it opens (Reducer_Web_SelectMenu.RA_Open),
    // and with its page dropped it never shows and never closes.
    private static readonly ClickSnapshot OrphanedMenu = Healthy with { IsWorldClickable = false, IsSelectMenuFlagged = true };

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void World_clicks_switched_off_by_a_menu_that_never_opened_are_given_back(RepairTrigger trigger)
    {
        var repair = ClickHeal.Decide(OrphanedMenu, trigger);

        Assert.True(repair.RestoreWorldClicks);
        Assert.True(repair.ClearSelectFlag);
        Assert.False(repair.CloseSelectMenu);
        Assert.False(repair.CloseTrapPanel);
    }

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void A_world_that_still_takes_clicks_is_left_alone(RepairTrigger trigger)
    {
        Assert.Equal(ClickRepair.None, ClickHeal.Decide(Healthy, trigger));
        Assert.False(ClickHeal.Decide(Healthy, trigger).IsNeeded);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void After_an_automatic_repair_a_panel_the_game_says_is_open_keeps_the_clicks(bool isSelectMenuOpen, bool isTrapPanelOpen)
    {
        var panelUp = Healthy with
        {
            IsWorldClickable = false, IsSelectMenuOpen = isSelectMenuOpen, IsTrapPanelOpen = isTrapPanelOpen, IsSelectMenuFlagged = isSelectMenuOpen,
        };

        Assert.Equal(ClickRepair.None, ClickHeal.Decide(panelUp, RepairTrigger.AfterRecovery));
    }

    [Fact]
    public void Ctrl_F12_closes_the_panels_holding_the_clicks_and_gives_them_back()
    {
        var bothUp = Healthy with { IsWorldClickable = false, IsSelectMenuOpen = true, IsTrapPanelOpen = true, IsSelectMenuFlagged = true };

        Assert.Equal(new ClickRepair(true, true, true, true), ClickHeal.Decide(bothUp, RepairTrigger.Manual));
    }

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void A_camera_still_flagging_an_object_menu_that_is_not_open_is_cleared(RepairTrigger trigger)
    {
        var staleFlag = Healthy with { IsSelectMenuFlagged = true };

        Assert.Equal(ClickRepair.None with { ClearSelectFlag = true }, ClickHeal.Decide(staleFlag, trigger));
    }

    [Fact]
    public void The_flag_of_an_object_menu_that_is_really_open_is_kept()
    {
        var menuUp = Healthy with { IsSelectMenuOpen = true, IsSelectMenuFlagged = true };

        Assert.Equal(ClickRepair.None, ClickHeal.Decide(menuUp, RepairTrigger.Manual));
    }
}
