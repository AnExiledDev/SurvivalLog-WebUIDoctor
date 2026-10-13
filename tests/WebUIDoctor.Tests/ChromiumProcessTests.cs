using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ChromiumProcessTests
{
    [Theory]
    [InlineData("\"Vuplex WebView.vuplex\" --log-severity=warning --vx-pipe-name=x", "browser")]
    [InlineData("\"Vuplex WebView.vuplex\" --type=renderer --lang=en-US", "renderer")]
    [InlineData("\"Vuplex WebView.vuplex\" --type=gpu-process --use-adapter-luid=0,83396", "gpu-process")]
    [InlineData("\"Vuplex WebView.vuplex\" --type=utility --utility-sub-type=network.mojom.NetworkService --lang=en-US", "utility:network.mojom.NetworkService")]
    [InlineData("\"Vuplex WebView.vuplex\" --type=utility --lang=en-US", "utility")]
    public void Names_the_process_role_so_a_dead_renderer_is_told_apart_from_a_dead_gpu_process(string commandLine, string expected)
    {
        Assert.Equal(expected, ChromiumProcess.KindOf(commandLine));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_unreadable_command_line_is_unknown_not_browser(string? commandLine)
    {
        Assert.Equal("unknown", ChromiumProcess.KindOf(commandLine));
    }

    [Theory]
    [InlineData("\"Vuplex WebView.vuplex\" --log-severity=warning --disable-web-security --allow-file-access-from-files --vx-pipe-name=x", true)]
    [InlineData("\"Vuplex WebView.vuplex\" --log-severity=warning --vx-pipe-name=x --allow-file-access-from-files --disable-web-security", true)]
    [InlineData("\"Vuplex WebView.vuplex\" --log-severity=warning --vx-pipe-name=x", false)]
    [InlineData("\"Vuplex WebView.vuplex\" --disable-web-security --vx-pipe-name=x", false)]
    [InlineData("\"Vuplex WebView.vuplex\" --disable-web-security-extra --allow-file-access-from-files-x", false)]
    [InlineData(null, false)]
    public void A_browser_started_without_both_page_access_flags_is_told_apart(string? commandLine, bool expected)
    {
        Assert.Equal(expected, ChromiumProcess.HasPageAccessFlags(commandLine));
    }

    [Theory]
    [InlineData(false, "--disable-web-security --allow-file-access-from-files")]
    [InlineData(true, "--disable-web-security --allow-file-access-from-files --no-sandbox")]
    public void A_relaunch_gets_the_same_arguments_the_game_started_chromium_with(bool isSandboxOff, string expected)
    {
        // The game's startup string, read from WebUILayer.Initialize: dropping --no-sandbox would start a browser
        // the game had already found could not run sandboxed.
        Assert.Equal(expected, ChromiumProcess.LaunchArguments(isSandboxOff));
    }
}
