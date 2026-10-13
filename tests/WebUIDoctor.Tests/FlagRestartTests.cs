using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class FlagRestartTests
{
    private const string BrowserWithFlags = "\"Vuplex WebView.vuplex\" --disable-web-security --allow-file-access-from-files --vx-pipe-name=x";
    private const string BrowserWithoutFlags = "\"Vuplex WebView.vuplex\" --log-severity=warning --vx-pipe-name=x";
    private const string Renderer = "\"Vuplex WebView.vuplex\" --type=renderer --lang=en-US";

    private static (FlagCheckOutcome Outcome, bool IsRestartSpent) CheckAfterRecovery(string?[] commandLines, bool isRestartSpent) =>
        FlagRestart.Decide(commandLines, isRecoveryUnderWay: false, isRestartSpent);

    [Fact]
    public void A_browser_relaunched_without_its_flags_is_restarted_once()
    {
        // The crash seen in play: the recovered browser had no flags and every page mod stopped working.
        var (outcome, isSpent) = CheckAfterRecovery(new[] { BrowserWithoutFlags, Renderer }, isRestartSpent: false);

        Assert.Equal(FlagCheckOutcome.RestartBrowser, outcome);
        Assert.True(isSpent);
    }

    [Fact]
    public void A_restart_that_did_not_bring_the_flags_back_is_never_repeated()
    {
        var (_, isSpent) = CheckAfterRecovery(new[] { BrowserWithoutFlags }, isRestartSpent: false);

        for (var check = 0; check < 3; check++)
        {
            var (outcome, stillSpent) = CheckAfterRecovery(new[] { BrowserWithoutFlags }, isSpent);

            Assert.Equal(FlagCheckOutcome.GiveUp, outcome);
            isSpent = stillSpent;
        }
    }

    [Fact]
    public void A_browser_seen_with_its_flags_earns_the_restart_back_for_the_next_crash()
    {
        var (_, isSpent) = CheckAfterRecovery(new[] { BrowserWithoutFlags }, isRestartSpent: false);
        var (healed, isSpentAfterHeal) = CheckAfterRecovery(new[] { BrowserWithFlags }, isSpent);
        var (nextCrash, _) = CheckAfterRecovery(new[] { BrowserWithoutFlags }, isSpentAfterHeal);

        Assert.Equal(FlagCheckOutcome.FlagsPresent, healed);
        Assert.Equal(FlagCheckOutcome.RestartBrowser, nextCrash);
    }

    [Fact]
    public void Only_the_browser_process_is_judged_not_its_renderers()
    {
        var (outcome, _) = CheckAfterRecovery(new[] { BrowserWithFlags, Renderer, null }, isRestartSpent: false);

        Assert.Equal(FlagCheckOutcome.FlagsPresent, outcome);
    }

    [Fact]
    public void A_flagless_browser_left_over_beside_a_flagged_one_still_gets_the_restart()
    {
        // The game's terminate can time out and leave the old flagless browser running next to the new one.
        // The check cannot tell which browser hosts the UI, so every browser must carry the flags.
        var (outcome, isSpent) = CheckAfterRecovery(new[] { BrowserWithFlags, BrowserWithoutFlags }, isRestartSpent: false);

        Assert.Equal(FlagCheckOutcome.RestartBrowser, outcome);
        Assert.True(isSpent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_readable_browser_restarts_nothing_and_leaves_the_restart_as_it_was(bool isRestartSpent)
    {
        // A command line that could not be read is no evidence the flags are missing.
        var (outcome, isSpent) = CheckAfterRecovery(new[] { Renderer, null }, isRestartSpent);

        Assert.Equal(FlagCheckOutcome.BrowserNotFound, outcome);
        Assert.Equal(isRestartSpent, isSpent);
    }

    [Fact]
    public void A_recovery_under_way_is_never_interrupted_by_a_restart()
    {
        var (outcome, isSpent) = FlagRestart.Decide(new[] { BrowserWithoutFlags }, isRecoveryUnderWay: true, isRestartSpent: false);

        Assert.Equal(FlagCheckOutcome.RecoveryUnderWay, outcome);
        Assert.False(isSpent);
    }
}
