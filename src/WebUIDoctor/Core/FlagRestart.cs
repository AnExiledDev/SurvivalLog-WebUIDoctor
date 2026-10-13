using System.Collections.Generic;
using System.Linq;

namespace WebUIDoctor.Core;

public enum FlagCheckOutcome
{
    /// <summary>Every browser process carries the page-access flags.</summary>
    FlagsPresent,

    /// <summary>No browser process with a readable command line: nothing to judge, so nothing is done.</summary>
    BrowserNotFound,

    /// <summary>The UI is down or recovering again: that recovery gets its own check when it ends.</summary>
    RecoveryUnderWay,

    /// <summary>The flags are missing: restart the browser once through the game's own recovery, which hands them over.</summary>
    RestartBrowser,

    /// <summary>The flags are still missing after the one restart: leave the UI up rather than restart it again.</summary>
    GiveUp,
}

/// <summary>
/// Decides, after a recovery, whether the relaunched Chromium came up without the page-access flags and gets one
/// restart for it. The restart is spent by restarting and given back only once a browser is seen with its flags, so
/// after a restart that does not bring them back, no further crash gets a forced restart until a browser shows its
/// flags again. It can never loop.
/// </summary>
public static class FlagRestart
{
    /// <param name="chromiumCommandLines">Every Chromium process under the game; null for one whose command line could not be read.</param>
    /// <param name="isRecoveryUnderWay">The game's UI is crashed or recovering, or the doctor's own restart is running.</param>
    /// <param name="isRestartSpent">The one restart was used and no browser has shown its flags since.</param>
    public static (FlagCheckOutcome Outcome, bool IsRestartSpent) Decide(
        IEnumerable<string?> chromiumCommandLines, bool isRecoveryUnderWay, bool isRestartSpent)
    {
        if (isRecoveryUnderWay) return (FlagCheckOutcome.RecoveryUnderWay, isRestartSpent);

        var browsers = chromiumCommandLines.Where(commandLine => ChromiumProcess.KindOf(commandLine) == "browser").ToList();

        if (browsers.Count == 0) return (FlagCheckOutcome.BrowserNotFound, isRestartSpent);

        if (browsers.All(ChromiumProcess.HasPageAccessFlags)) return (FlagCheckOutcome.FlagsPresent, false);

        return isRestartSpent ? (FlagCheckOutcome.GiveUp, true) : (FlagCheckOutcome.RestartBrowser, true);
    }
}
