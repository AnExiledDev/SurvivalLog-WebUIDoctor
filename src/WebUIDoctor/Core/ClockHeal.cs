namespace WebUIDoctor.Core;

/// <summary>Where the watch is: just watching, or rechecking after one of its two fixes.</summary>
public enum ClockStep
{
    Watching,
    AfterGameResync,
    AfterDirectSync,
}

public enum ClockAction
{
    None,

    /// <summary>Ask the game to send its time to the page, the same way its 30-second resync does.</summary>
    AskGameToResync,

    /// <summary>The game's own resync did not land, so post the time into the clock's frame ourselves.</summary>
    PostSyncDirectly,

    ReportGameResyncFixedIt,
    ReportDirectSyncFixedIt,
    ReportStillDrifted,
}

/// <summary>
/// Decides the next fix for a drifted page clock. The game's own resync goes first: if it lands, the drift was
/// a missed message; if only the direct post lands, the game's link to the page is broken.
/// </summary>
public static class ClockHeal
{
    public static (ClockAction Action, ClockStep Next) Decide(ClockStep step, ClockVerdict verdict) => (step, verdict) switch
    {
        (_, ClockVerdict.Unreadable) => (ClockAction.None, ClockStep.Watching),
        (ClockStep.Watching, ClockVerdict.InStep) => (ClockAction.None, ClockStep.Watching),
        (ClockStep.Watching, ClockVerdict.Drifted) => (ClockAction.AskGameToResync, ClockStep.AfterGameResync),
        (ClockStep.AfterGameResync, ClockVerdict.InStep) => (ClockAction.ReportGameResyncFixedIt, ClockStep.Watching),
        (ClockStep.AfterGameResync, ClockVerdict.Drifted) => (ClockAction.PostSyncDirectly, ClockStep.AfterDirectSync),
        (ClockStep.AfterDirectSync, ClockVerdict.InStep) => (ClockAction.ReportDirectSyncFixedIt, ClockStep.Watching),
        (ClockStep.AfterDirectSync, ClockVerdict.Drifted) => (ClockAction.ReportStillDrifted, ClockStep.Watching),
        _ => (ClockAction.None, ClockStep.Watching),
    };
}
