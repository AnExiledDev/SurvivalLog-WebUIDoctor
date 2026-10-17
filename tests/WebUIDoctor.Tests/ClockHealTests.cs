using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ClockHealTests
{
    [Fact]
    public void A_clock_in_step_is_left_alone()
    {
        Assert.Equal((ClockAction.None, ClockStep.Watching), ClockHeal.Decide(ClockStep.Watching, ClockVerdict.InStep));
    }

    [Fact]
    public void A_drift_first_asks_the_game_for_its_own_resync()
    {
        Assert.Equal((ClockAction.AskGameToResync, ClockStep.AfterGameResync), ClockHeal.Decide(ClockStep.Watching, ClockVerdict.Drifted));
    }

    [Fact]
    public void When_the_game_resync_lands_that_is_reported_and_nothing_more_is_done()
    {
        Assert.Equal((ClockAction.ReportGameResyncFixedIt, ClockStep.Watching), ClockHeal.Decide(ClockStep.AfterGameResync, ClockVerdict.InStep));
    }

    [Fact]
    public void When_the_game_resync_does_not_land_the_time_is_posted_directly()
    {
        Assert.Equal((ClockAction.PostSyncDirectly, ClockStep.AfterDirectSync), ClockHeal.Decide(ClockStep.AfterGameResync, ClockVerdict.Drifted));
    }

    [Fact]
    public void The_direct_post_is_tried_once_and_its_outcome_reported_either_way()
    {
        Assert.Equal((ClockAction.ReportDirectSyncFixedIt, ClockStep.KeepingInStep), ClockHeal.Decide(ClockStep.AfterDirectSync, ClockVerdict.InStep));
        Assert.Equal((ClockAction.ReportStillDrifted, ClockStep.Watching), ClockHeal.Decide(ClockStep.AfterDirectSync, ClockVerdict.Drifted));
    }

    [Theory]
    [InlineData(ClockStep.Watching)]
    [InlineData(ClockStep.AfterGameResync)]
    [InlineData(ClockStep.AfterDirectSync)]
    public void An_unreadable_page_never_triggers_a_fix(ClockStep step)
    {
        Assert.Equal((ClockAction.None, ClockStep.Watching), ClockHeal.Decide(step, ClockVerdict.Unreadable));
    }

    [Theory]
    [InlineData(ClockVerdict.InStep)]
    [InlineData(ClockVerdict.Drifted)]
    public void Once_only_the_direct_post_works_every_check_posts_the_time(ClockVerdict verdict)
    {
        Assert.Equal((ClockAction.PostSyncQuietly, ClockStep.KeepingInStep), ClockHeal.Decide(ClockStep.KeepingInStep, verdict));
    }

    [Fact]
    public void An_unreadable_page_does_not_forget_the_broken_link()
    {
        Assert.Equal((ClockAction.None, ClockStep.KeepingInStep), ClockHeal.Decide(ClockStep.KeepingInStep, ClockVerdict.Unreadable));
    }
}
