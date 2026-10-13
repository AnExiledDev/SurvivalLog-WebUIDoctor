using System.Linq;
using System.Text.RegularExpressions;

namespace WebUIDoctor.Core;

public static class ChromiumProcess
{
    private static readonly Regex TypeArg = new(@"--type=([\w-]+)", RegexOptions.Compiled);
    private static readonly Regex UtilitySubType = new(@"--utility-sub-type=([\w.]+)", RegexOptions.Compiled);

    /// <summary>
    /// The flags the game starts Chromium with so its Root page can reach into its page frames. Without them each
    /// file:// frame is its own origin: the game still works through postMessage, but every mod that edits a page breaks.
    /// </summary>
    public const string PageAccessFlags = "--disable-web-security --allow-file-access-from-files";

    /// <summary>
    /// The exact argument string the game hands Chromium at startup: the page-access flags, plus --no-sandbox when the
    /// game found the sandbox unusable. A relaunch gets the same string, so it starts the browser the game would.
    /// </summary>
    public static string LaunchArguments(bool isSandboxOff) => isSandboxOff ? PageAccessFlags + " --no-sandbox" : PageAccessFlags;

    /// <summary>"browser", "renderer", "gpu-process", "utility:network.mojom.NetworkService", or "unknown".</summary>
    public static string KindOf(string? commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return "unknown";

        var type = TypeArg.Match(commandLine);

        if (!type.Success) return "browser";

        var kind = type.Groups[1].Value;

        if (kind != "utility") return kind;

        var sub = UtilitySubType.Match(commandLine);

        return sub.Success ? "utility:" + sub.Groups[1].Value : kind;
    }

    public static bool HasPageAccessFlags(string? commandLine) =>
        commandLine != null && PageAccessFlags.Split(' ').All(flag => Regex.IsMatch(commandLine, Regex.Escape(flag) + @"(\s|""|$)"));
}
