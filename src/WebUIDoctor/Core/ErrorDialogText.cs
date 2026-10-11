using System;
using System.Linq;

namespace WebUIDoctor.Core;

/// <summary>Recognises the game's "automatic recovery has failed" dialog by its on-screen text.</summary>
public static class ErrorDialogText
{
    // English as seen on the user's screen, plus the Chinese the game is written in, in case the language changes.
    private static readonly string[] Markers =
    {
        "interface display error",
        "automatic recovery has failed",
        "界面显示异常",
        "界面显示错误",
        "自动恢复失败",
    };

    public static bool IsRecoveryFailureDialog(string? text) =>
        !string.IsNullOrWhiteSpace(text) && Markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
