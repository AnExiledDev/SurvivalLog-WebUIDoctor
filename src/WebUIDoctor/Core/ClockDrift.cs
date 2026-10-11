using System;

namespace WebUIDoctor.Core;

public enum ClockVerdict
{
    /// <summary>The page clock shows the game's time.</summary>
    InStep,

    /// <summary>The page clock shows a different time than the game runs on.</summary>
    Drifted,

    /// <summary>No time could be read off the page, so nothing is known.</summary>
    Unreadable,
}

/// <param name="DriftMinutes">Page minus game, in game minutes, the short way round midnight. 0 when unreadable.</param>
/// <param name="TrapDetail">What the page says about its trap list, passed through for the log.</param>
public readonly record struct ClockReading(ClockVerdict Verdict, int DriftMinutes, string PageTime, string TrapDetail);

/// <summary>
/// Compares the clock the CoreUI1 page draws with the game's own time. The page runs its own clock between
/// the game's resyncs (every 30 seconds), so a page that stops receiving them keeps whatever offset it had,
/// for the rest of the launch: the day then seems to end at 3am when the game ends it at midnight.
/// </summary>
public static class ClockDrift
{
    /// <summary>The page rounds down to 10 minutes and resyncs every 30 real seconds; anything past this is not jitter.</summary>
    public const int ToleranceMinutes = 30;

    private const int SecondsPerDay = 86400;
    private const int MinutesPerDay = 1440;

    public static int SecondOfDay(int gameTotalSeconds) => ((gameTotalSeconds % SecondsPerDay) + SecondsPerDay) % SecondsPerDay;

    /// <summary>Reads the page script's reply, "ok|HH:MM|trap detail", against the game's second of the day.</summary>
    public static ClockReading Read(string? reply, int gameSecondOfDay)
    {
        var parts = (reply ?? "").Split('|');

        if (parts.Length != 3 || parts[0] != "ok") return new ClockReading(ClockVerdict.Unreadable, 0, reply ?? "(none)", "");

        var trapDetail = parts[2];

        if (ParseMinuteOfDay(parts[1]) is not { } pageMinute) return new ClockReading(ClockVerdict.Unreadable, 0, parts[1], trapDetail);

        var gameMinute = SecondOfDay(gameSecondOfDay) / 60 / 10 * 10;
        var drift = Wrap(pageMinute - gameMinute);
        var verdict = Math.Abs(drift) > ToleranceMinutes ? ClockVerdict.Drifted : ClockVerdict.InStep;

        return new ClockReading(verdict, drift, parts[1], trapDetail);
    }

    private static int? ParseMinuteOfDay(string text)
    {
        var parts = text.Split(':');

        if (parts.Length != 2 || !int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute)) return null;
        if (hour is < 0 or > 23 || minute is < 0 or > 59) return null;

        return hour * 60 + minute;
    }

    private static int Wrap(int minutes)
    {
        var wrapped = ((minutes % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;

        return wrapped >= MinutesPerDay / 2 ? wrapped - MinutesPerDay : wrapped;
    }
}
