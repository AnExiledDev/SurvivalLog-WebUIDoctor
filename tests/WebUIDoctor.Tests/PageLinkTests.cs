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
        MissingModScripts: Array.Empty<string>());

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
