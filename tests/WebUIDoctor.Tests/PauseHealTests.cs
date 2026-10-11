using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class PauseHealTests
{
    private static readonly PauseSnapshot LeftoverPause = new(
        IsWorldPaused: true, IsCameraPaused: true, IsRenderFrozen: false, IsSpeedPaused: false, IsInLegitPauseModal: false, UserUiPauseHolds: 1);

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void A_world_held_paused_while_the_speed_control_says_running_is_resumed(RepairTrigger trigger)
    {
        Assert.Equal(PauseVerdict.Stuck, PauseHeal.Decide(LeftoverPause, trigger));
    }

    [Fact]
    public void A_still_frame_left_on_screen_alone_counts_as_stuck()
    {
        var frozenView = LeftoverPause with { IsWorldPaused = false, IsCameraPaused = false, IsRenderFrozen = true };

        Assert.Equal(PauseVerdict.Stuck, PauseHeal.Decide(frozenView, RepairTrigger.AfterRecovery));
    }

    [Theory]
    [InlineData(RepairTrigger.Manual)]
    [InlineData(RepairTrigger.AfterRecovery)]
    public void A_pause_the_player_chose_with_the_speed_control_is_never_overridden(RepairTrigger trigger)
    {
        Assert.Equal(PauseVerdict.PausedByPlayer, PauseHeal.Decide(LeftoverPause with { IsSpeedPaused = true }, trigger));
    }

    [Fact]
    public void After_an_automatic_repair_a_window_the_game_says_is_open_keeps_its_pause()
    {
        Assert.Equal(PauseVerdict.PausedByOpenWindow, PauseHeal.Decide(LeftoverPause with { IsInLegitPauseModal = true }, RepairTrigger.AfterRecovery));
    }

    [Fact]
    public void After_an_automatic_repair_an_open_window_that_holds_no_pause_is_stuck()
    {
        // 2026-10-11 01:58:24: world, camera and render all held, legitModal=True, uiPauseHolds=0. The game's own
        // sleep-deadlock diagnostic reads holds=0 with a paused world as a real deadlock.
        var orphanedModal = LeftoverPause with { IsInLegitPauseModal = true, UserUiPauseHolds = 0 };

        Assert.Equal(PauseVerdict.Stuck, PauseHeal.Decide(orphanedModal, RepairTrigger.AfterRecovery));
    }

    [Fact]
    public void Ctrl_F12_resumes_even_when_the_game_thinks_a_window_is_open()
    {
        Assert.Equal(PauseVerdict.Stuck, PauseHeal.Decide(LeftoverPause with { IsInLegitPauseModal = true }, RepairTrigger.Manual));
    }

    [Fact]
    public void A_running_game_is_left_alone_even_with_the_speed_control_on_pause()
    {
        var running = new PauseSnapshot(false, false, false, IsSpeedPaused: true, IsInLegitPauseModal: false, UserUiPauseHolds: 0);

        Assert.Equal(PauseVerdict.Running, PauseHeal.Decide(running, RepairTrigger.Manual));
    }
}
