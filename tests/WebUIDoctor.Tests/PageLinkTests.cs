using System;
using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class PageLinkTests
{
    private static readonly PageProbeResult AllWorking = new(
        IsLayerWebViewLive: true,
        OtherLiveWebViews: 0,
        LayerUpdatesDuringProbe: 600,
        HasScriptReply: true,
        HasMessageReply: true,
        MissingModScripts: Array.Empty<string>(),
        BlockedFrames: Array.Empty<BlockedFrame>());

    /// <summary>The Root page's own words when Chromium runs without the page-access flags (crafting-hub.log, 2026-10-12).</summary>
    private static readonly BlockedFrame CrossOriginCooking = new(
        "Cooking.html",
        "SecurityError: Failed to read a named property 'document' from 'Window': Blocked a frame with origin \"null\" from accessing a cross-origin frame.");

    [Fact]
    public void A_page_that_answers_on_both_channels_with_every_mod_script_present_is_healthy()
    {
        Assert.Equal(PageLinkVerdict.Healthy, PageLink.Decide(AllWorking));
    }

    [Fact]
    public void A_dead_webview_is_blamed_before_anything_that_depends_on_it()
    {
        var dead = AllWorking with { IsLayerWebViewLive = false, HasScriptReply = false, HasMessageReply = false };

        Assert.Equal(PageLinkVerdict.WebViewNotLive, PageLink.Decide(dead));
    }

    [Fact]
    public void A_second_live_webview_means_the_mods_may_be_sending_to_the_wrong_one()
    {
        var twoWebViews = AllWorking with { OtherLiveWebViews = 1, HasScriptReply = false, HasMessageReply = false };

        Assert.Equal(PageLinkVerdict.StaleWebViewReference, PageLink.Decide(twoWebViews));
    }

    [Fact]
    public void A_layer_that_stopped_updating_starves_every_mod_even_when_the_page_answers()
    {
        var stalled = AllWorking with { LayerUpdatesDuringProbe = 0 };

        Assert.Equal(PageLinkVerdict.LayerNotUpdating, PageLink.Decide(stalled));
    }

    [Theory]
    [InlineData(false, false, PageLinkVerdict.NoReplies)]
    [InlineData(false, true, PageLinkVerdict.ScriptRepliesLost)]
    [InlineData(true, false, PageLinkVerdict.PageMessagesLost)]
    public void Each_lost_reply_channel_is_named_on_its_own(bool hasScriptReply, bool hasMessageReply, PageLinkVerdict expected)
    {
        var probe = AllWorking with { HasScriptReply = hasScriptReply, HasMessageReply = hasMessageReply };

        Assert.Equal(expected, PageLink.Decide(probe));
    }

    [Fact]
    public void A_page_that_cannot_read_its_frames_is_broken_even_with_every_channel_and_mod_script_working()
    {
        // The probes after the crash seen in play answered on both channels with every mod global set and were called
        // Healthy, while the cooking mod's window failed on exactly this error.
        var blocked = AllWorking with { BlockedFrames = new[] { CrossOriginCooking } };

        Assert.Equal(PageLinkVerdict.FramesBlocked, PageLink.Decide(blocked));
    }

    [Fact]
    public void Blocked_frames_are_named_ahead_of_a_missing_mod_script_because_the_script_could_not_have_worked()
    {
        var both = AllWorking with { BlockedFrames = new[] { CrossOriginCooking }, MissingModScripts = new[] { "__projectCook" } };

        Assert.Equal(PageLinkVerdict.FramesBlocked, PageLink.Decide(both));
    }

    [Fact]
    public void Each_blocked_frame_is_read_from_the_reply_with_the_error_it_threw()
    {
        const string reply =
            "{\"id\":\"p2\",\"mods\":{\"__projectCook\":true},\"frames\":[\"CoreUI1.html\",\"Cooking.html\"]," +
            "\"blocked\":[{\"frame\":\"Cooking.html\",\"error\":\"SecurityError: Failed to read a named property 'document' from 'Window': " +
            "Blocked a frame with origin \\\"null\\\" from accessing a cross-origin frame.\"}]}";

        Assert.Equal(new[] { CrossOriginCooking }, PageLink.BlockedFrames(reply));
    }

    [Theory]
    [InlineData("{\"id\":\"p1\",\"frames\":[\"CoreUI1.html\"],\"blocked\":[]}")]
    [InlineData("{\"id\":\"p1\",\"frames\":[\"CoreUI1.html\"]}")]
    [InlineData("probe error: ReferenceError: x is not defined")]
    [InlineData("")]
    [InlineData(null)]
    public void A_reply_that_lists_no_blocked_frame_blocks_nothing(string? reply)
    {
        Assert.Empty(PageLink.BlockedFrames(reply));
    }

    [Fact]
    public void Working_channels_with_a_mod_script_absent_point_at_the_mod_not_the_webview()
    {
        var notInjected = AllWorking with { MissingModScripts = new[] { "__projectCook" } };

        Assert.Equal(PageLinkVerdict.ModScriptsMissing, PageLink.Decide(notInjected));
    }

    [Fact]
    public void Only_loaded_mods_whose_global_the_page_reports_false_count_as_missing()
    {
        const string reply = "{\"mods\":{\"__projectCook\":false,\"__bettertrade\":true,\"__itemtotals\":false}}";

        var missing = PageLink.MissingModScripts(reply, new[] { "__projectCook", "__bettertrade" });

        Assert.Equal(new[] { "__projectCook" }, missing);
    }

    [Fact]
    public void No_reply_reports_no_missing_scripts_because_that_is_a_channel_fault()
    {
        Assert.Empty(PageLink.MissingModScripts(null, new[] { "__projectCook" }));
    }
}
