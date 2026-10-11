using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ProcessTreeTests
{
    private const string Chromium = "Vuplex WebView.vuplex";
    private const int Game = 100;

    [Fact]
    public void Finds_the_browser_and_every_child_it_spawned()
    {
        var table = new[]
        {
            new ProcessEntry(Game, 1, "SurvivalLog.exe"),
            new ProcessEntry(200, Game, Chromium),
            new ProcessEntry(201, 200, Chromium),
            new ProcessEntry(202, 200, Chromium),
        };

        Assert.Equal(new[] { 200, 201, 202 }, ProcessTree.DescendantsNamed(table, Game, Chromium).OrderBy(p => p));
    }

    [Fact]
    public void Finds_chromium_below_a_non_chromium_parent()
    {
        var table = new[]
        {
            new ProcessEntry(300, Game, "crashpad_handler.exe"),
            new ProcessEntry(301, 300, Chromium),
        };

        Assert.Equal(new[] { 301 }, ProcessTree.DescendantsNamed(table, Game, Chromium));
    }

    [Fact]
    public void Never_touches_chromium_that_belongs_to_another_program()
    {
        var table = new[]
        {
            new ProcessEntry(200, Game, Chromium),
            new ProcessEntry(900, 50, Chromium),
            new ProcessEntry(901, 900, Chromium),
        };

        Assert.Equal(new[] { 200 }, ProcessTree.DescendantsNamed(table, Game, Chromium));
    }

    [Fact]
    public void Matches_the_exe_name_case_insensitively_like_windows()
    {
        var table = new[] { new ProcessEntry(200, Game, "VUPLEX WEBVIEW.VUPLEX") };

        Assert.Equal(new[] { 200 }, ProcessTree.DescendantsNamed(table, Game, Chromium));
    }

    [Fact]
    public void Terminates_on_reused_pids_that_form_a_parent_loop()
    {
        // Windows reuses pids, so a stale parent pid can point back into the tree.
        var table = new[]
        {
            new ProcessEntry(200, Game, Chromium),
            new ProcessEntry(201, 200, Chromium),
            new ProcessEntry(200, 201, Chromium),
            new ProcessEntry(Game, Game, "SurvivalLog.exe"),
        };

        Assert.Equal(new[] { 200, 201 }, ProcessTree.DescendantsNamed(table, Game, Chromium).OrderBy(p => p));
    }
}
