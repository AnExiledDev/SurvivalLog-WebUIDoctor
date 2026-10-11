using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ClockDriftTests
{
    private const int Midnight = 0;

    private static int GameSeconds(int hour, int minute, int second = 0) => hour * 3600 + minute * 60 + second;

    [Fact]
    public void A_page_clock_three_hours_ahead_of_the_game_is_drifted()
    {
        var reading = ClockDrift.Read("ok|03:10|0", GameSeconds(0, 12));

        Assert.Equal(ClockVerdict.Drifted, reading.Verdict);
        Assert.Equal(180, reading.DriftMinutes);
    }

    [Fact]
    public void The_page_shows_ten_minute_steps_so_a_game_clock_inside_the_same_step_is_in_step()
    {
        Assert.Equal(ClockVerdict.InStep, ClockDrift.Read("ok|14:20|0", GameSeconds(14, 29, 59)).Verdict);
    }

    [Fact]
    public void Half_an_hour_apart_is_still_in_step_because_the_game_only_resyncs_every_30_seconds()
    {
        Assert.Equal(ClockVerdict.InStep, ClockDrift.Read("ok|14:50|0", GameSeconds(14, 20)).Verdict);
        Assert.Equal(ClockVerdict.Drifted, ClockDrift.Read("ok|15:00|0", GameSeconds(14, 20)).Verdict);
    }

    [Fact]
    public void Drift_across_midnight_is_measured_the_short_way_round()
    {
        var pageJustPastMidnight = ClockDrift.Read("ok|00:10|0", GameSeconds(23, 50));
        var pageJustBeforeMidnight = ClockDrift.Read("ok|23:50|0", GameSeconds(0, 10));

        Assert.Equal(20, pageJustPastMidnight.DriftMinutes);
        Assert.Equal(-20, pageJustBeforeMidnight.DriftMinutes);
        Assert.Equal(ClockVerdict.InStep, pageJustPastMidnight.Verdict);
    }

    [Fact]
    public void A_page_clock_behind_the_game_is_drifted_too()
    {
        var reading = ClockDrift.Read("ok|06:00|0", GameSeconds(9, 0));

        Assert.Equal(ClockVerdict.Drifted, reading.Verdict);
        Assert.Equal(-180, reading.DriftMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no frame")]
    [InlineData("error: SecurityError")]
    [InlineData("ok||0")]
    [InlineData("ok|25:00|0")]
    [InlineData("ok|ab:cd|0")]
    public void A_reply_without_a_readable_time_is_never_called_drifted(string? reply)
    {
        Assert.Equal(ClockVerdict.Unreadable, ClockDrift.Read(reply, Midnight).Verdict);
    }

    [Fact]
    public void The_reply_carries_what_the_page_says_about_its_trap_list_for_the_log()
    {
        Assert.Equal("active=1 traps=5 panel=1", ClockDrift.Read("ok|08:00|active=1 traps=5 panel=1", GameSeconds(8, 0)).TrapDetail);
    }

    [Fact]
    public void The_game_time_is_taken_within_the_day_whatever_day_it_is()
    {
        Assert.Equal(GameSeconds(1, 30), ClockDrift.SecondOfDay(36 * 86400 + GameSeconds(1, 30)));
    }
}
